using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/family")]
[Authorize]
public class FamilyController(AppDbContext db) : ControllerBase
{
    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");

    // ── Get my family ─────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetMyFamily()
    {
        var uid = CurrentUserId();
        var user = await db.Users
            .Include(u => u.Spouse)
            .Include(u => u.Children).ThenInclude(c => c.ClassChildren.Where(cc => !cc.IsRemoved))
            .FirstOrDefaultAsync(u => u.Id == uid);
        if (user == null) return NotFound();

        return Ok(new
        {
            spouse = user.Spouse == null ? null : new
            {
                user.Spouse.Id,
                user.Spouse.FirstName,
                user.Spouse.LastName,
                user.Spouse.Email,
            },
            children = user.Children.Select(c => new
            {
                c.Id,
                c.FirstName,
                c.LastName,
                inAnyClass = c.ClassChildren.Any(cc => !cc.IsRemoved),
            }),
        });
    }

    // ── Spouse ────────────────────────────────────────────────────────────────

    [HttpPost("spouse")]
    public async Task<IActionResult> SetSpouse([FromBody] SpouseRequest req)
    {
        var uid = CurrentUserId();
        var me = await db.Users.FindAsync(uid);
        if (me == null) return NotFound();

        // Find matching user by email
        var normalEmail = req.Email.Trim().ToLower();
        var match = await db.Users.FirstOrDefaultAsync(u =>
            u.Email == normalEmail &&
            u.FirstName.ToLower() == req.FirstName.Trim().ToLower() &&
            u.LastName.ToLower() == req.LastName.Trim().ToLower() &&
            u.Id != uid);

        if (match == null)
            return BadRequest("No member found with that name and email. The spouse must already have an account.");

        me.SpouseUserId = match.Id;
        await db.SaveChangesAsync();

        return Ok(new { match.Id, match.FirstName, match.LastName, match.Email });
    }

    [HttpDelete("spouse")]
    public async Task<IActionResult> RemoveSpouse()
    {
        var uid = CurrentUserId();
        var me = await db.Users.FindAsync(uid);
        if (me == null) return NotFound();
        me.SpouseUserId = null;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Children ──────────────────────────────────────────────────────────────

    [HttpPost("children")]
    public async Task<IActionResult> AddChild([FromBody] FamilyChildRequest req)
    {
        var uid = CurrentUserId();

        var firstName = req.FirstName.Trim();
        var lastName = req.LastName.Trim();

        var child = new Child { FirstName = firstName, LastName = lastName, ParentUserId = uid };
        db.Children.Add(child);
        await db.SaveChangesAsync();

        // Check for name collisions with existing children (excluding the one just created)
        var matches = await db.Children
            .Where(c => c.Id != child.Id && (
                c.FirstName.ToLower() == firstName.ToLower() ||
                c.LastName.ToLower() == lastName.ToLower()))
            .ToListAsync();

        foreach (var match in matches)
        {
            var alreadySuggested = await db.ChildLinkSuggestions
                .AnyAsync(s => s.RequestingUserId == uid && s.NewChildId == child.Id && s.SuggestedChildId == match.Id);
            if (!alreadySuggested)
            {
                db.ChildLinkSuggestions.Add(new ChildLinkSuggestion
                {
                    RequestingUserId = uid,
                    NewChildId = child.Id,
                    SuggestedChildId = match.Id,
                });
            }
        }
        if (matches.Count > 0) await db.SaveChangesAsync();

        return Ok(new { child.Id, child.FirstName, child.LastName, hasSuggestions = matches.Count > 0 });
    }

    [HttpDelete("children/{childId}")]
    public async Task<IActionResult> RemoveChild(int childId)
    {
        var uid = CurrentUserId();
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == childId && c.ParentUserId == uid);
        if (child == null) return NotFound();
        child.ParentUserId = null;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Unclassed children (for banners on classes/attendance pages) ──────────

    [HttpGet("unclassed-children")]
    public async Task<IActionResult> GetUnclassedChildren()
    {
        var uid = CurrentUserId();
        var children = await db.Children
            .Where(c => c.ParentUserId == uid)
            .Include(c => c.ClassChildren)
            .ToListAsync();

        var unclassed = children.Where(c => !c.ClassChildren.Any(cc => !cc.IsRemoved)).ToList();
        return Ok(unclassed.Select(c => new { c.Id, c.FirstName, c.LastName }));
    }

    // ── Admin: child link suggestions ─────────────────────────────────────────

    [HttpGet("child-link-suggestions")]
    public async Task<IActionResult> GetChildLinkSuggestions()
    {
        if (!IsSuperAdmin()) return Forbid();

        var suggestions = await db.ChildLinkSuggestions
            .Where(s => !s.IsResolved)
            .Include(s => s.RequestingUser)
            .Include(s => s.NewChild)
            .Include(s => s.SuggestedChild)
            .ToListAsync();

        return Ok(suggestions.Select(s => new
        {
            s.Id,
            requestingUser = new { s.RequestingUser.Id, s.RequestingUser.FirstName, s.RequestingUser.LastName },
            newChild = new { s.NewChild.Id, s.NewChild.FirstName, s.NewChild.LastName },
            suggestedChild = new { s.SuggestedChild.Id, s.SuggestedChild.FirstName, s.SuggestedChild.LastName },
            s.CreatedAt,
        }));
    }

    [HttpPost("child-link-suggestions/{id}/link")]
    public async Task<IActionResult> LinkChild(int id)
    {
        if (!IsSuperAdmin()) return Forbid();

        var suggestion = await db.ChildLinkSuggestions
            .Include(s => s.NewChild)
            .Include(s => s.SuggestedChild)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsResolved);
        if (suggestion == null) return NotFound();

        // Transfer the parent link from the new child to the suggested (existing) child
        suggestion.SuggestedChild.ParentUserId = suggestion.RequestingUserId;
        // Remove the newly created duplicate child record
        db.Children.Remove(suggestion.NewChild);
        // Resolve all suggestions involving this new child
        var related = await db.ChildLinkSuggestions
            .Where(s => s.NewChildId == suggestion.NewChildId).ToListAsync();
        foreach (var s in related) s.IsResolved = true;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("child-link-suggestions/{id}/dismiss")]
    public async Task<IActionResult> DismissSuggestion(int id)
    {
        if (!IsSuperAdmin()) return Forbid();

        var suggestion = await db.ChildLinkSuggestions.FindAsync(id);
        if (suggestion == null) return NotFound();
        suggestion.IsResolved = true;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record SpouseRequest(string FirstName, string LastName, string Email);
public record FamilyChildRequest(string FirstName, string LastName);

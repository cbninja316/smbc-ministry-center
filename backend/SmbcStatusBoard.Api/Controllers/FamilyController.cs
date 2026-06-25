using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;
using SmbcStatusBoard.Api.Services;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/family")]
[Authorize]
public class FamilyController(AppDbContext db, EmailService email, IConfiguration config) : ControllerBase
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

        var normalEmail = req.Email.Trim().ToLower();
        var firstName = req.FirstName.Trim();
        var lastName = req.LastName.Trim();

        // If a member with that email already exists...
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == normalEmail && u.Id != uid);
        if (existing != null)
        {
            me.SpouseUserId = existing.Id;
            await db.SaveChangesAsync();

            // Active user — link both directions, no email needed
            if (existing.IsActive)
            {
                existing.SpouseUserId = uid;
                await db.SaveChangesAsync();
                return Ok(new { existing.Id, existing.FirstName, existing.LastName, existing.Email, invited = false });
            }

            // Inactive (previously invited but never activated) — expire old tokens and resend
            var oldTokens = await db.InviteTokens.Where(t => t.UserId == existing.Id && !t.Used).ToListAsync();
            foreach (var t in oldTokens) t.Used = true;

            var newToken = Guid.NewGuid().ToString("N");
            db.InviteTokens.Add(new InviteToken { UserId = existing.Id, Token = newToken, ExpiresAt = DateTime.UtcNow.AddDays(7) });
            await db.SaveChangesAsync();

            var frontendUrlExisting = config["App:NextFrontendUrl"]?.Trim()
                ?? config["App:FrontendUrl"]?.Split(',')[0].Trim() ?? "";
            var joinLinkExisting = $"{frontendUrlExisting}/setup-password?token={newToken}";
            var myNameExisting = $"{me.FirstName} {me.LastName}".Trim();
            if (string.IsNullOrEmpty(myNameExisting)) myNameExisting = me.Username;
            try { await email.SendSpouseInviteAsync(existing.Email, existing.FirstName, myNameExisting, existing.Username, joinLinkExisting); } catch { }

            return Ok(new { existing.Id, existing.FirstName, existing.LastName, existing.Email, invited = true });
        }

        // No account found — create inactive user and send invite
        // Username rule: Capitalize(first) + Capitalize(last) — letters only, first char upper, rest lower
        static string CapWord(string s)
        {
            var letters = new string(s.Where(char.IsLetter).ToArray());
            return letters.Length == 0 ? "" : char.ToUpper(letters[0]) + letters[1..].ToLower();
        }
        var baseUsername = CapWord(firstName) + CapWord(lastName);
        if (string.IsNullOrEmpty(baseUsername)) baseUsername = "Member";
        var username = baseUsername;
        var suffix = 1;
        while (await db.Users.AnyAsync(u => u.Username == username))
            username = baseUsername + suffix++;

        var newUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = normalEmail,
            Username = username,
            Role = UserRole.Member,
            IsActive = false,
            EmailVerified = false,
            AllowedItemTypes = string.Empty,
        };
        db.Users.Add(newUser);
        await db.SaveChangesAsync();

        var token = Guid.NewGuid().ToString("N");
        db.InviteTokens.Add(new InviteToken
        {
            UserId = newUser.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        });

        me.SpouseUserId = newUser.Id;
        await db.SaveChangesAsync();

        // Prefer NextFrontendUrl (single value) then first entry of FrontendUrl
        var frontendUrl = config["App:NextFrontendUrl"]?.Trim()
            ?? config["App:FrontendUrl"]?.Split(',')[0].Trim()
            ?? "";
        var joinLink = $"{frontendUrl}/setup-password?token={token}";
        var myName = $"{me.FirstName} {me.LastName}".Trim();
        if (string.IsNullOrEmpty(myName)) myName = me.Username;
        try { await email.SendSpouseInviteAsync(normalEmail, firstName, myName, username, joinLink); } catch { }

        return Ok(new { newUser.Id, newUser.FirstName, newUser.LastName, newUser.Email, invited = true });
    }

    [HttpDelete("spouse")]
    public async Task<IActionResult> RemoveSpouse()
    {
        var uid = CurrentUserId();
        var me = await db.Users.Include(u => u.Spouse).FirstOrDefaultAsync(u => u.Id == uid);
        if (me == null) return NotFound();
        // Clear both directions
        if (me.Spouse != null && me.Spouse.SpouseUserId == uid)
            me.Spouse.SpouseUserId = null;
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

        // Check for exact full-name collisions with existing children (excluding the one just created)
        var matches = await db.Children
            .Where(c => c.Id != child.Id &&
                c.FirstName.ToLower() == firstName.ToLower() &&
                c.LastName.ToLower() == lastName.ToLower())
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
            requestingUser = new {
                s.RequestingUser.Id,
                s.RequestingUser.FirstName,
                s.RequestingUser.LastName,
                s.RequestingUser.Username,
            },
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

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;
using SmbcStatusBoard.Api.Services;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/children")]
[Authorize]
public class ChildrenController(AppDbContext db, EmailService emailService, IConfiguration config) : ControllerBase
{
    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");
    private bool CanManageClasses() =>
        IsSuperAdmin() ||
        (User.FindFirstValue("AllowedItemTypes") ?? "").Split(',').Contains("Classes", StringComparer.OrdinalIgnoreCase);
    private bool CanManageAttendance() =>
        IsSuperAdmin() ||
        (User.FindFirstValue("AllowedItemTypes") ?? "").Split(',').Contains("Attendance", StringComparer.OrdinalIgnoreCase);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!CanManageClasses()) return Forbid();
        var children = await db.Children
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .ToListAsync();
        return Ok(children.Select(c => new {
            c.Id, c.FirstName, c.LastName, c.BirthDate,
            Gender = c.Gender?.ToString(),
            c.CreatedAt, c.ParentUserId, c.LinkedUserId,
            c.IsVerified, c.VerifiedAt,
            CheckInToken = (string?)null // don't expose token in list
        }));
    }

    [HttpGet("unverified")]
    public async Task<IActionResult> GetUnverified()
    {
        if (!CanManageAttendance()) return Forbid();
        // Only include children who are in at least one class that requires a pass
        var childIdsNeedingPass = await db.ClassChildren
            .Where(cc => !cc.IsRemoved && cc.Class.RequiresChildPass)
            .Select(cc => cc.ChildId)
            .Distinct()
            .ToListAsync();
        var children = await db.Children
            .Where(c => c.ParentUserId != null && !c.IsVerified && childIdsNeedingPass.Contains(c.Id))
            .Include(c => c.ParentUser)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
        return Ok(children.Select(c => new {
            c.Id, c.FirstName, c.LastName, c.BirthDate,
            Gender = c.Gender?.ToString(),
            c.ParentUserId,
            ParentName = c.ParentUser != null ? $"{c.ParentUser.FirstName} {c.ParentUser.LastName}" : null,
            c.IsVerified
        }));
    }

    [HttpPost("{id}/verify")]
    public async Task<IActionResult> Verify(int id)
    {
        if (!CanManageAttendance()) return Forbid();
        var child = await db.Children.FindAsync(id);
        if (child == null) return NotFound();
        child.IsVerified = true;
        child.VerifiedAt = DateTime.UtcNow;
        child.VerifiedByUserId = CurrentUserId();
        child.CheckInToken ??= Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
        return Ok(new { child.Id, child.IsVerified, child.CheckInToken, child.VerifiedAt });
    }

    [HttpGet("{id}/checkin-token")]
    public async Task<IActionResult> GetCheckInToken(int id)
    {
        var userId = CurrentUserId();
        var child = await db.Children.FindAsync(id);
        if (child == null) return NotFound();
        // Allow: the parent, their spouse, attendance admins, and super admins
        bool isParent = child.ParentUserId == userId;
        bool isSpouse = false;
        if (!isParent && child.ParentUserId.HasValue)
        {
            var parent = await db.Users
                .Where(u => u.Id == child.ParentUserId.Value)
                .Select(u => new { u.SpouseUserId })
                .FirstOrDefaultAsync();
            isSpouse = parent?.SpouseUserId == userId;
        }
        if (!isParent && !isSpouse && !CanManageAttendance())
            return Forbid();
        if (!child.IsVerified)
            return BadRequest(new { message = "This child has not been verified for check-in yet." });
        if (child.CheckInToken == null)
        {
            child.CheckInToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
        }
        return Ok(new { token = child.CheckInToken, child.FirstName, child.LastName, BirthDate = child.BirthDate?.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ChildPayload req)
    {
        if (!CanManageClasses()) return Forbid();
        var child = new Child
        {
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            BirthDate = req.BirthDate is not null ? DateOnly.Parse(req.BirthDate) : null,
            Gender = req.Gender is not null && Enum.TryParse<Models.Gender>(req.Gender, true, out var cg) ? cg : null,
        };
        db.Children.Add(child);
        await db.SaveChangesAsync();
        return Ok(new { child.Id, child.FirstName, child.LastName, child.BirthDate, Gender = child.Gender?.ToString(), child.CreatedAt });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ChildPayload req)
    {
        if (!CanManageClasses()) return Forbid();
        var child = await db.Children.FindAsync(id);
        if (child == null) return NotFound();
        child.FirstName = req.FirstName.Trim();
        child.LastName = req.LastName.Trim();
        child.BirthDate = req.BirthDate is not null ? DateOnly.Parse(req.BirthDate) : null;
        if (req.Gender is not null && Enum.TryParse<Models.Gender>(req.Gender, true, out var ug)) child.Gender = ug;
        await db.SaveChangesAsync();
        return Ok(new { child.Id, child.FirstName, child.LastName, child.BirthDate, Gender = child.Gender?.ToString() });
    }

    [HttpPost("{id}/promote-to-member")]
    public async Task<IActionResult> PromoteToMember(int id, [FromBody] PromoteChildRequest req)
    {
        if (!IsSuperAdmin()) return Forbid();

        var child = await db.Children.FindAsync(id);
        if (child == null) return NotFound();

        var email = req.Email.Trim().ToLower();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "A user with that email already exists." });

        static string Capitalize(string s)
        {
            var letters = new string(s.Where(char.IsLetter).ToArray());
            return letters.Length == 0 ? "" : char.ToUpper(letters[0]) + letters[1..].ToLower();
        }
        var baseUsername = Capitalize(child.FirstName) + Capitalize(child.LastName);
        var username = baseUsername;
        var suffix = 1;
        while (await db.Users.AnyAsync(u => u.Username == username))
            username = baseUsername + suffix++;

        var role = req.Role?.ToLower() switch
        {
            "superadmin" => UserRole.SuperAdmin,
            "admin" => UserRole.Admin,
            _ => UserRole.Member
        };
        var allowedTypes = (role == UserRole.SuperAdmin || role == UserRole.Member)
            ? ""
            : string.Join(",", req.AllowedItemTypes ?? []);

        var user = new User
        {
            FirstName = child.FirstName,
            LastName = child.LastName,
            Username = username,
            Email = email,
            Role = role,
            AllowedItemTypes = allowedTypes,
            IsActive = false
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        db.InviteTokens.Add(new InviteToken
        {
            Token = token,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddHours(48)
        });

        // Keep the child record so they remain in the family group; link to the new user account
        child.LinkedUserId = user.Id;
        await db.SaveChangesAsync();

        var siteUrl = config["App:NextFrontendUrl"] ?? config["App:SiteUrl"] ?? config["App:FrontendUrl"];
        var inviteLink = $"{siteUrl}/setup-password?token={token}";
        await emailService.SendInviteAsync(user.Email, user.Username, inviteLink, user.ChurchId);

        return Ok(new { message = "Invite sent.", userId = user.Id, username });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsSuperAdmin()) return Forbid();
        var child = await db.Children.FindAsync(id);
        if (child == null) return NotFound();

        // Remove dependent records that don't cascade
        var suggestions = await db.ChildLinkSuggestions
            .Where(s => s.NewChildId == id || s.SuggestedChildId == id)
            .ToListAsync();
        db.ChildLinkSuggestions.RemoveRange(suggestions);

        var registrations = await db.EventRegistrations
            .Where(r => r.ChildId == id)
            .ToListAsync();
        db.EventRegistrations.RemoveRange(registrations);

        db.Children.Remove(child);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record ChildPayload(string FirstName, string LastName, string? BirthDate, string? Gender = null);
public record PromoteChildRequest(string Email, string? Role = null, List<string>? AllowedItemTypes = null);

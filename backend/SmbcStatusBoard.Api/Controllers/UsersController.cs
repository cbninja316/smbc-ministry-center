using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.DTOs;
using SmbcStatusBoard.Api.Models;
using SmbcStatusBoard.Api.Services;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class UsersController(AppDbContext db, EmailService emailService, IConfiguration config) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous] // auth checked manually below so AssignVolunteers users can also call this
    public async Task<IActionResult> GetAll()
    {
        // SuperAdmin gets full list; AssignVolunteers gets active users only (for volunteer picker)
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var allowed = User.FindFirst("AllowedItemTypes")?.Value ?? "";
        var canAssign = role == "SuperAdmin" || allowed.Split(',', StringSplitOptions.RemoveEmptyEntries).Contains("AssignVolunteers");

        if (!canAssign)
            return Forbid();

        var users = await db.Users.Select(u => new
        {
            u.Id,
            u.Username,
            u.FirstName,
            u.LastName,
            u.Email,
            Role = u.Role.ToString(),
            u.IsActive,
            AllowedItemTypes = u.AllowedItemTypes.Split(',', StringSplitOptions.RemoveEmptyEntries),
            u.CreatedAt,
            MembershipStatus = u.MembershipStatus.ToString(),
            JoinedBy = u.JoinedBy == null ? (string?)null : u.JoinedBy.ToString(),
            u.MembershipDate,
            u.HasLeft,
            u.IsDeceased,
            BirthDate = u.BirthDate == null ? (string?)null : u.BirthDate.Value.ToString("yyyy-MM-dd"),
            Gender = u.Gender == null ? (string?)null : u.Gender.ToString(),
        }).ToListAsync();

        return Ok(users);
    }

    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
            return BadRequest(new { message = "First and last name are required." });

        if (await db.Users.AnyAsync(u => u.Email == req.Email))
            return Conflict(new { message = "A user with that email already exists." });

        if (!Enum.TryParse<UserRole>(req.Role, true, out var role))
            return BadRequest(new { message = "Invalid role." });

        // Generate username: FirstLast with first letter of each capitalised, letters only
        static string Capitalize(string s) {
            var letters = new string(s.Where(char.IsLetter).ToArray());
            return letters.Length == 0 ? "" : char.ToUpper(letters[0]) + letters[1..].ToLower();
        }
        var baseUsername = Capitalize(req.FirstName.Trim()) + Capitalize(req.LastName.Trim());
        var username = baseUsername;
        var suffix = 1;
        while (await db.Users.AnyAsync(u => u.Username == username))
            username = baseUsername + suffix++;

        var user = new User
        {
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            Username = username,
            Email = req.Email.Trim().ToLower(),
            Role = role,
            AllowedItemTypes = string.Join(',', req.AllowedItemTypes),
            IsActive = false
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var invite = new InviteToken
        {
            Token = token,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddHours(48)
        };

        db.InviteTokens.Add(invite);
        await db.SaveChangesAsync();

        // Use NextFrontendUrl so invite links go to the Next.js app, not WordPress.
        var siteUrl = config["App:NextFrontendUrl"] ?? config["App:SiteUrl"] ?? config["App:FrontendUrl"];
        var inviteLink = $"{siteUrl}/setup-password?token={token}";

        await emailService.SendInviteAsync(user.Email, user.Username, inviteLink);

        return Ok(new { message = "Invite sent.", userId = user.Id });
    }

    [HttpPut("{id}/permissions")]
    public async Task<IActionResult> UpdatePermissions(int id, [FromBody] string[] allowedItemTypes)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        user.AllowedItemTypes = string.Join(',', allowedItemTypes);
        await db.SaveChangesAsync();

        return Ok(new { message = "Permissions updated." });
    }

    [HttpPut("{id}/role")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateUserRoleRequest req)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (!Enum.TryParse<UserRole>(req.Role, true, out var newRole))
            return BadRequest(new { message = "Invalid role." });

        var oldRole = user.Role;
        user.Role = newRole;
        await db.SaveChangesAsync();

        if (newRole != UserRole.Member && newRole != oldRole)
        {
            var frontendUrl = config["App:NextFrontendUrl"] ?? "https://oneaccord.southmoorebc.org";
            var displayName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrEmpty(displayName)) displayName = user.Username;
            await emailService.SendRolePromotionAsync(user.Email, displayName, newRole.ToString(), frontendUrl + "/login");
        }

        return Ok(new { message = "Role updated." });
    }

    // POST /api/users/{id}/verify — SuperAdmin manually activates a user who couldn't verify by email
    [HttpPost("{id}/verify")]
    public async Task<IActionResult> ManualVerify(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        user.EmailVerified = true;
        user.IsActive = true;

        // Mark any pending verification tokens as used
        var tokens = await db.EmailVerificationTokens
            .Where(t => t.UserId == id && !t.Used)
            .ToListAsync();
        foreach (var t in tokens) t.Used = true;

        await db.SaveChangesAsync();
        return Ok(new { message = "User verified and activated." });
    }

    [HttpPut("{id}/profile")]
    public async Task<IActionResult> UpdateProfile(int id, [FromBody] UpdateProfileRequest req)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(req.FirstName)) user.FirstName = req.FirstName.Trim();
        if (!string.IsNullOrWhiteSpace(req.LastName)) user.LastName = req.LastName.Trim();
        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            var normalEmail = req.Email.Trim().ToLower();
            if (normalEmail != user.Email && await db.Users.AnyAsync(u => u.Email == normalEmail && u.Id != id))
                return Conflict(new { message = "A user with that email already exists." });
            user.Email = normalEmail;
        }
        user.BirthDate = req.BirthDate is not null ? DateOnly.Parse(req.BirthDate) : user.BirthDate;
        if (req.Gender is not null && Enum.TryParse<Models.Gender>(req.Gender, true, out var gender))
            user.Gender = gender;

        await db.SaveChangesAsync();
        return Ok(new { message = "Profile updated." });
    }

    [HttpPut("{id}/membership")]
    public async Task<IActionResult> UpdateMembership(int id, [FromBody] UpdateMembershipRequest req)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (!Enum.TryParse<Models.MembershipStatus>(req.MembershipStatus, true, out var status))
            return BadRequest(new { message = "Invalid membership status." });

        user.MembershipStatus = status;
        user.JoinedBy = null;
        if (req.JoinedBy is not null && Enum.TryParse<Models.JoinedBy>(req.JoinedBy, true, out var joinedBy))
            user.JoinedBy = joinedBy;
        user.MembershipDate = req.MembershipDate.HasValue ? DateOnly.FromDateTime(req.MembershipDate.Value) : null;
        user.HasLeft = req.HasLeft;
        user.IsDeceased = req.IsDeceased;

        await db.SaveChangesAsync();
        return Ok(new { message = "Membership updated." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/users/by-email?email=... — any authenticated user can look up by email (for spouse autofill)
    [HttpGet("by-email")]
    [Authorize]
    public async Task<IActionResult> GetByEmail([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return BadRequest();
        var normalized = email.Trim().ToLower();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized && u.IsActive);
        if (user == null) return NotFound();
        return Ok(new
        {
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            Gender = user.Gender == null ? (string?)null : user.Gender.ToString(),
            BirthDate = user.BirthDate?.ToString("yyyy-MM-dd"),
        });
    }
}

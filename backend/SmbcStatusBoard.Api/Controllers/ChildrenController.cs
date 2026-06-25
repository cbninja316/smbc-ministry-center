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
    private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");
    private bool CanManageClasses() =>
        IsSuperAdmin() ||
        (User.FindFirstValue("AllowedItemTypes") ?? "").Split(',').Contains("Classes", StringComparer.OrdinalIgnoreCase);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!CanManageClasses()) return Forbid();
        var children = await db.Children
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .ToListAsync();
        return Ok(children);
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
        };
        db.Children.Add(child);
        await db.SaveChangesAsync();
        return Ok(new { child.Id, child.FirstName, child.LastName, child.BirthDate, child.CreatedAt });
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

        var user = new User
        {
            FirstName = child.FirstName,
            LastName = child.LastName,
            Username = username,
            Email = email,
            Role = UserRole.Member,
            AllowedItemTypes = "",
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

        db.Children.Remove(child);
        await db.SaveChangesAsync();

        var siteUrl = config["App:NextFrontendUrl"] ?? config["App:SiteUrl"] ?? config["App:FrontendUrl"];
        var inviteLink = $"{siteUrl}/setup-password?token={token}";
        await emailService.SendInviteAsync(user.Email, user.Username, inviteLink);

        return Ok(new { message = "Invite sent.", userId = user.Id, username });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsSuperAdmin()) return Forbid();
        var child = await db.Children.FindAsync(id);
        if (child == null) return NotFound();
        db.Children.Remove(child);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record ChildPayload(string FirstName, string LastName, string? BirthDate);
public record PromoteChildRequest(string Email);

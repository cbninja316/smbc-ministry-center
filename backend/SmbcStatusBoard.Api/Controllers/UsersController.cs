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
            u.Email,
            Role = u.Role.ToString(),
            u.IsActive,
            AllowedItemTypes = u.AllowedItemTypes.Split(',', StringSplitOptions.RemoveEmptyEntries),
            u.CreatedAt
        }).ToListAsync();

        return Ok(users);
    }

    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteUserRequest req)
    {
        if (await db.Users.AnyAsync(u => u.Email == req.Email))
            return Conflict(new { message = "A user with that email already exists." });

        if (await db.Users.AnyAsync(u => u.Username == req.Username))
            return Conflict(new { message = "That username is already taken." });

        if (!Enum.TryParse<UserRole>(req.Role, true, out var role))
            return BadRequest(new { message = "Invalid role." });

        var user = new User
        {
            Username = req.Username,
            Email = req.Email,
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

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

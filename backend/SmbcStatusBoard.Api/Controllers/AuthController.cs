using System.Security.Claims;
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
public class AuthController(AppDbContext db, TokenService tokenService, EmailService emailService, IConfiguration config) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username && u.IsActive);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid credentials." });

        var token = tokenService.Generate(user);
        var allowed = user.AllowedItemTypes.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return Ok(new LoginResponse(token, user.Username, user.Role.ToString(), allowed));
    }

    [HttpPost("set-password")]
    public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest req)
    {
        var invite = await db.InviteTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == req.Token && !t.Used && t.ExpiresAt > DateTime.UtcNow);

        if (invite is null)
            return BadRequest(new { message = "Invalid or expired invite link." });

        invite.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        invite.User.IsActive = true;
        invite.Used = true;

        await db.SaveChangesAsync();

        var token = tokenService.Generate(invite.User);
        var allowed = invite.User.AllowedItemTypes.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return Ok(new LoginResponse(token, invite.User.Username, invite.User.Role.ToString(), allowed));
    }

    [HttpGet("validate-invite")]
    public async Task<IActionResult> ValidateInvite([FromQuery] string token)
    {
        var invite = await db.InviteTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && !t.Used && t.ExpiresAt > DateTime.UtcNow);

        if (invite is null)
            return BadRequest(new { message = "Invalid or expired invite link." });

        return Ok(new { username = invite.User.Username, email = invite.User.Email });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        // Always return 200 — security through obscurity
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email && u.IsActive);
        if (user is not null)
        {
            var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var resetToken = new PasswordResetToken
            {
                Token = token,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };
            db.PasswordResetTokens.Add(resetToken);
            await db.SaveChangesAsync();

            var frontendUrl = config["App:NextFrontendUrl"] ?? "https://oneaccord.southmoorebc.org";
            var resetLink = $"{frontendUrl}/reset-password?token={token}";
            await emailService.SendPasswordResetAsync(user.Email, user.Username, resetLink);
        }

        return Ok(new { message = "If that email exists in our system, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var resetToken = await db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == req.Token && !t.Used && t.ExpiresAt > DateTime.UtcNow);

        if (resetToken is null)
            return BadRequest(new { message = "Invalid or expired reset link." });

        resetToken.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        resetToken.Used = true;

        await db.SaveChangesAsync();

        var jwtToken = tokenService.Generate(resetToken.User);
        var allowed = resetToken.User.AllowedItemTypes.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return Ok(new LoginResponse(jwtToken, resetToken.User.Username, resetToken.User.Role.ToString(), allowed));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await db.SaveChangesAsync();

        return Ok(new { message = "Password updated successfully." });
    }
}

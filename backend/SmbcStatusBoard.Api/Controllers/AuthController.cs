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
public class AuthController(AppDbContext db, TokenService tokenService) : ControllerBase
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
}

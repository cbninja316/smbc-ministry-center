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
        var identifier = req.Username.Trim();
        var user = await db.Users.FirstOrDefaultAsync(u =>
            (u.Username == identifier || u.Email == identifier.ToLower()) && u.IsActive);
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

        // If someone linked this user as their spouse, set the reverse link too
        if (invite.User.SpouseUserId.HasValue)
        {
            var linker = await db.Users.FindAsync(invite.User.SpouseUserId.Value);
            if (linker != null && linker.SpouseUserId == null)
                linker.SpouseUserId = invite.User.Id;
        }
        else
        {
            // Check if anyone set this user as their spouse (the other direction)
            var linker = await db.Users.FirstOrDefaultAsync(u => u.SpouseUserId == invite.User.Id);
            if (linker != null)
                invite.User.SpouseUserId = linker.Id;
        }

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

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
            return BadRequest(new { message = "First and last name are required." });

        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
            return BadRequest(new { message = "A valid email address is required." });

        if (req.Password.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters." });

        if (req.Password != req.ConfirmPassword)
            return BadRequest(new { message = "Passwords do not match." });

        var normalizedEmail = req.Email.Trim().ToLower();
        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail))
            return Conflict(new { message = "An account with that email already exists." });

        // Generate unique username: FirstLast (first letter uppercase, rest lowercase, letters only)
        static string Capitalize(string s) {
            var letters = new string(s.Where(char.IsLetter).ToArray());
            return letters.Length == 0 ? "" : char.ToUpper(letters[0]) + letters[1..].ToLower();
        }
        var baseUsername = Capitalize(req.FirstName.Trim()) + Capitalize(req.LastName.Trim());

        var username = baseUsername;
        var suffix = 1;
        while (await db.Users.AnyAsync(u => u.Username == username))
            username = baseUsername + suffix++;

        DateOnly? birthDate = null;
        if (!string.IsNullOrWhiteSpace(req.BirthDate) && DateOnly.TryParse(req.BirthDate, out var parsedBirth))
            birthDate = parsedBirth;

        var user = new User
        {
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            Username = username,
            Email = req.Email.Trim().ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = UserRole.Member,
            IsActive = false,
            EmailVerified = false,
            AllowedItemTypes = string.Empty,
            BirthDate = birthDate
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var tokenStr = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var verifyToken = new EmailVerificationToken
        {
            Token = tokenStr,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };
        db.EmailVerificationTokens.Add(verifyToken);
        await db.SaveChangesAsync();

        var frontendUrl = config["App:NextFrontendUrl"] ?? "https://oneaccord.southmoorebc.org";
        var verifyLink = $"{frontendUrl}/verify-email?token={tokenStr}";
        try
        {
            await emailService.SendEmailVerificationAsync(user.Email, $"{user.FirstName} {user.LastName}", verifyLink);
        }
        catch (Exception ex)
        {
            // Log the error so it's visible in server output
            Console.Error.WriteLine($"[Register] Failed to send verification email to {user.Email}: {ex.Message}");
            return StatusCode(500, new { message = $"Account created but verification email could not be sent: {ex.Message}. Please contact an admin." });
        }

        return Ok(new { message = "Account created. Please check your email to verify your account.", username });
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        var verifyToken = await db.EmailVerificationTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && !t.Used && t.ExpiresAt > DateTime.UtcNow);

        if (verifyToken is null)
            return BadRequest(new { message = "Invalid or expired verification link." });

        verifyToken.User.EmailVerified = true;
        verifyToken.User.IsActive = true;
        verifyToken.Used = true;
        await db.SaveChangesAsync();

        var jwtToken = tokenService.Generate(verifyToken.User);
        var allowed = verifyToken.User.AllowedItemTypes.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return Ok(new LoginResponse(jwtToken, verifyToken.User.Username, verifyToken.User.Role.ToString(), allowed));
    }

    // POST /api/auth/resend-verification — allows an unverified user to request a new link
    [HttpPost("resend-verification")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest req)
    {
        var email = req.Email?.Trim().ToLower();
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Email is required." });

        // Always return 200 to avoid leaking whether an email exists
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && !u.EmailVerified);
        if (user is null) return Ok(new { message = "If that email has a pending account, a new verification link has been sent." });

        // Invalidate old tokens
        var oldTokens = await db.EmailVerificationTokens
            .Where(t => t.UserId == user.Id && !t.Used)
            .ToListAsync();
        foreach (var t in oldTokens) t.Used = true;

        var tokenStr = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            Token = tokenStr,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        await db.SaveChangesAsync();

        var frontendUrl = config["App:NextFrontendUrl"] ?? "https://oneaccord.southmoorebc.org";
        var verifyLink = $"{frontendUrl}/verify-email?token={tokenStr}";
        try
        {
            await emailService.SendEmailVerificationAsync(user.Email, $"{user.FirstName} {user.LastName}", verifyLink);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ResendVerification] Failed to send email to {user.Email}: {ex.Message}");
            return StatusCode(500, new { message = $"Could not send verification email: {ex.Message}" });
        }

        return Ok(new { message = "If that email has a pending account, a new verification link has been sent." });
    }

    [HttpPatch("birthdate")]
    [Authorize]
    public async Task<IActionResult> UpdateBirthDate([FromBody] UpdateBirthDateRequest req)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        if (string.IsNullOrWhiteSpace(req.BirthDate))
            user.BirthDate = null;
        else if (DateOnly.TryParse(req.BirthDate, out var parsed))
            user.BirthDate = parsed;
        else
            return BadRequest(new { message = "Invalid date format." });

        await db.SaveChangesAsync();
        return Ok(new { message = "Birthdate updated." });
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

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var uid = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(uid);
        if (user == null) return NotFound();
        return Ok(new { user.Id, user.FirstName, user.LastName, user.Username, user.Email });
    }
}

public record ResendVerificationRequest(string? Email);

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;
using SmbcStatusBoard.Api.Services;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChurchController(AppDbContext db, TokenService tokenService, EmailService emailService, IConfiguration config) : ControllerBase
{
    private int? CurrentChurchId()
    {
        var s = User.FindFirstValue("ChurchId");
        return int.TryParse(s, out var id) ? id : null;
    }

    // GET /api/church/list — public list of churches (id + name only, for registration)
    [HttpGet("list")]
    public async Task<IActionResult> List()
    {
        var churches = await db.Churches
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();
        return Ok(churches);
    }

    // GET /api/church/info — current user's church info (logo, name)
    [HttpGet("info")]
    [Authorize]
    public async Task<IActionResult> Info()
    {
        var churchId = CurrentChurchId();
        if (churchId is null) return NotFound(new { message = "No church linked to this account." });

        var church = await db.Churches.FindAsync(churchId);
        if (church is null) return NotFound(new { message = "Church not found." });

        return Ok(new { church.Id, church.Name, church.LogoData, church.Slug });
    }

    // PUT /api/church/info — SuperAdmin: update church name and logo
    [HttpPut("info")]
    [Authorize]
    public async Task<IActionResult> UpdateInfo([FromBody] UpdateChurchRequest req)
    {
        if (User.FindFirstValue(ClaimTypes.Role) != "SuperAdmin") return Forbid();

        var churchId = CurrentChurchId();
        if (churchId is null) return BadRequest(new { message = "No church linked to your account." });

        var church = await db.Churches.FindAsync(churchId);
        if (church is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(req.Name)) church.Name = req.Name.Trim();
        if (req.LogoData is not null) church.LogoData = string.IsNullOrEmpty(req.LogoData) ? null : req.LogoData;

        await db.SaveChangesAsync();
        return Ok(new { church.Id, church.Name, church.LogoData, church.Slug });
    }

    // POST /api/church/register — public: create a new church + super admin account
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] ChurchRegisterRequest req)
    {
        // Validate church
        if (string.IsNullOrWhiteSpace(req.ChurchName))
            return BadRequest(new { message = "Church name is required." });

        // Validate admin
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

        // Create church
        var slug = Regex.Replace(req.ChurchName.Trim().ToLower(), @"[^a-z0-9]+", "-").Trim('-');
        var slugBase = slug;
        var slugSuffix = 1;
        while (await db.Churches.AnyAsync(c => c.Slug == slug))
            slug = slugBase + "-" + slugSuffix++;

        var church = new Church
        {
            Name = req.ChurchName.Trim(),
            Slug = slug,
            LogoData = string.IsNullOrEmpty(req.LogoData) ? null : req.LogoData,
            Status = "Pending",
        };
        db.Churches.Add(church);
        await db.SaveChangesAsync();

        // Create super admin user
        static string Capitalize(string s)
        {
            var letters = new string(s.Where(char.IsLetter).ToArray());
            return letters.Length == 0 ? "" : char.ToUpper(letters[0]) + letters[1..].ToLower();
        }
        var baseUsername = Capitalize(req.FirstName.Trim()) + Capitalize(req.LastName.Trim());
        var username = baseUsername;
        var userSuffix = 1;
        while (await db.Users.AnyAsync(u => u.Username == username))
            username = baseUsername + userSuffix++;

        var user = new User
        {
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            Username = username,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = UserRole.SuperAdmin,
            IsActive = true,
            EmailVerified = true,
            AllowedItemTypes = string.Empty,
            ChurchId = church.Id,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var jwtToken = tokenService.Generate(user);
        return Ok(new
        {
            token = jwtToken,
            username = user.Username,
            role = user.Role.ToString(),
            allowedItemTypes = Array.Empty<string>(),
            church = new { church.Id, church.Name, church.LogoData }
        });
    }
}

public record UpdateChurchRequest(string? Name, string? LogoData);
public record ChurchRegisterRequest(
    string ChurchName,
    string? LogoData,
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string ConfirmPassword
);

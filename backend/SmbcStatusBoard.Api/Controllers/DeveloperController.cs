using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;
using SmbcStatusBoard.Api.Services;
using System.Security.Claims;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Developer")]
public class DeveloperController(AppDbContext db, TokenService tokenService) : ControllerBase
{
    // GET /api/developer/churches/pending — churches awaiting approval
    [HttpGet("churches/pending")]
    public async Task<IActionResult> PendingChurches()
    {
        var churches = await db.Churches
            .Where(c => c.Status == "Pending")
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.LogoData,
                c.Slug,
                c.CreatedAt,
                adminEmail = c.Users.Where(u => u.Role == UserRole.SuperAdmin).Select(u => u.Email).FirstOrDefault(),
                adminName = c.Users.Where(u => u.Role == UserRole.SuperAdmin)
                    .Select(u => u.FirstName + " " + u.LastName).FirstOrDefault()
            })
            .ToListAsync();

        return Ok(churches);
    }

    // POST /api/developer/churches/{id}/approve
    [HttpPost("churches/{id}/approve")]
    public async Task<IActionResult> ApproveChurch(int id)
    {
        var church = await db.Churches.FindAsync(id);
        if (church is null) return NotFound();

        church.Status = "Approved";
        await db.SaveChangesAsync();

        return Ok(new { church.Id, church.Name, church.LogoData, church.Slug, church.Status, church.CreatedAt });
    }

    // POST /api/developer/churches/{id}/deny
    [HttpPost("churches/{id}/deny")]
    public async Task<IActionResult> DenyChurch(int id, [FromBody] DenyChurchRequest req)
    {
        var church = await db.Churches.FindAsync(id);
        if (church is null) return NotFound();

        church.Status = "Denied";
        church.DeniedReason = req.Reason;
        await db.SaveChangesAsync();

        return Ok(new { church.Id, church.Name, church.LogoData, church.Slug, church.Status, church.DeniedReason, church.CreatedAt });
    }

    // GET /api/developer/churches — all churches with users
    [HttpGet("churches")]
    public async Task<IActionResult> AllChurches()
    {
        var churches = await db.Churches
            .Include(c => c.Users)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.LogoData,
                c.Status,
                c.CreatedAt,
                userCount = c.Users.Count,
                users = c.Users.Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    role = u.Role.ToString()
                })
            })
            .ToListAsync();

        return Ok(churches);
    }

    // POST /api/developer/impersonate/{userId}
    [HttpPost("impersonate/{userId}")]
    public async Task<IActionResult> Impersonate(int userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return NotFound(new { message = "User not found." });

        var extraClaims = new[] { new Claim("DevSession", "true") };
        var token = tokenService.Generate(user, extraClaims);

        var allowedItemTypes = string.IsNullOrEmpty(user.AllowedItemTypes)
            ? Array.Empty<string>()
            : user.AllowedItemTypes.Split(',', StringSplitOptions.RemoveEmptyEntries);

        return Ok(new
        {
            token,
            username = user.Username,
            role = user.Role.ToString(),
            allowedItemTypes
        });
    }
}

public record DenyChurchRequest(string? Reason);

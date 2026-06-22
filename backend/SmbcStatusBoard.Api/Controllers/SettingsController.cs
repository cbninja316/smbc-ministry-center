using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController(AppDbContext db) : ControllerBase
{
    private bool IsSuperAdmin() =>
        User.FindFirstValue(ClaimTypes.Role) == "SuperAdmin";

    // GET /api/settings/stripe — SuperAdmin: returns current stripe config
    [HttpGet("stripe")]
    public async Task<IActionResult> GetStripeSettings()
    {
        if (!IsSuperAdmin()) return Forbid();

        var pub = await db.AppSettings.FindAsync("Stripe:PublishableKey");
        var sec = await db.AppSettings.FindAsync("Stripe:SecretKey");

        return Ok(new
        {
            publishableKey = pub?.Value ?? "",
            secretKeySet = !string.IsNullOrEmpty(sec?.Value),
        });
    }

    // PUT /api/settings/stripe — SuperAdmin: saves stripe keys
    [HttpPut("stripe")]
    public async Task<IActionResult> PutStripeSettings([FromBody] StripeSettingsRequest req)
    {
        if (!IsSuperAdmin()) return Forbid();

        await Upsert("Stripe:PublishableKey", req.PublishableKey ?? "");

        if (!string.IsNullOrEmpty(req.SecretKey))
            await Upsert("Stripe:SecretKey", req.SecretKey);

        await db.SaveChangesAsync();
        return Ok(new { message = "Stripe settings saved." });
    }

    // GET /api/settings/stripe/public-key — any authenticated user: returns publishable key only
    [HttpGet("stripe/public-key")]
    public async Task<IActionResult> GetPublicKey()
    {
        var pub = await db.AppSettings.FindAsync("Stripe:PublishableKey");
        return Ok(new { publishableKey = pub?.Value ?? "" });
    }

    private async Task Upsert(string key, string value)
    {
        var existing = await db.AppSettings.FindAsync(key);
        if (existing is null)
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        else
            existing.Value = value;
    }
}

public record StripeSettingsRequest(string? PublishableKey, string? SecretKey);

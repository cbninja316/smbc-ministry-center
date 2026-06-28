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

    // ── PraiseCharts settings ─────────────────────────────────────────────────

    [HttpGet("praisecharts")]
    public async Task<IActionResult> GetPraiseChartsSettings()
    {
        if (!IsSuperAdmin()) return Forbid();
        var ck = await db.AppSettings.FindAsync("PraiseCharts:ConsumerKey");
        var at = await db.AppSettings.FindAsync("PraiseCharts:AccessToken");
        return Ok(new
        {
            consumerKeySet = !string.IsNullOrEmpty(ck?.Value),
            accessTokenSet = !string.IsNullOrEmpty(at?.Value),
        });
    }

    [HttpPut("praisecharts")]
    public async Task<IActionResult> PutPraiseChartsSettings([FromBody] PraiseChartsSettingsRequest req)
    {
        if (!IsSuperAdmin()) return Forbid();
        if (!string.IsNullOrEmpty(req.ConsumerKey)) await Upsert("PraiseCharts:ConsumerKey", req.ConsumerKey);
        if (!string.IsNullOrEmpty(req.ConsumerSecret)) await Upsert("PraiseCharts:ConsumerSecret", req.ConsumerSecret);
        if (!string.IsNullOrEmpty(req.AccessToken)) await Upsert("PraiseCharts:AccessToken", req.AccessToken);
        if (!string.IsNullOrEmpty(req.AccessSecret)) await Upsert("PraiseCharts:AccessSecret", req.AccessSecret);
        await db.SaveChangesAsync();
        return Ok(new { message = "PraiseCharts settings saved." });
    }

    [HttpDelete("praisecharts/disconnect")]
    public async Task<IActionResult> DisconnectPraiseCharts()
    {
        if (!IsSuperAdmin()) return Forbid();
        foreach (var key in new[] { "PraiseCharts:AccessToken", "PraiseCharts:AccessSecret" })
        {
            var s = await db.AppSettings.FindAsync(key);
            if (s != null) db.AppSettings.Remove(s);
        }
        await db.SaveChangesAsync();
        return Ok(new { message = "PraiseCharts account disconnected." });
    }

    // GET /api/settings/printer
    [HttpGet("printer")]
    public async Task<IActionResult> GetPrinterSettings()
    {
        if (!IsSuperAdmin()) return Forbid();
        var name = await db.AppSettings.FindAsync("Printer:Name");
        var ip = await db.AppSettings.FindAsync("Printer:IpAddress");
        var model = await db.AppSettings.FindAsync("Printer:Model");
        var stickerSize = await db.AppSettings.FindAsync("Printer:StickerSize");
        return Ok(new
        {
            name = name?.Value ?? "",
            ipAddress = ip?.Value ?? "",
            model = model?.Value ?? "",
            stickerSize = stickerSize?.Value ?? "2.25x1.25",
        });
    }

    // PUT /api/settings/printer
    [HttpPut("printer")]
    public async Task<IActionResult> PutPrinterSettings([FromBody] PrinterSettingsRequest req)
    {
        if (!IsSuperAdmin()) return Forbid();
        await Upsert("Printer:Name", req.Name ?? "");
        await Upsert("Printer:IpAddress", req.IpAddress ?? "");
        await Upsert("Printer:Model", req.Model ?? "");
        await Upsert("Printer:StickerSize", req.StickerSize ?? "2.25x1.25");
        await db.SaveChangesAsync();
        return Ok(new { message = "Printer settings saved." });
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
public record PrinterSettingsRequest(string? Name, string? IpAddress, string? Model, string? StickerSize);
public record PraiseChartsSettingsRequest(string? ConsumerKey, string? ConsumerSecret, string? AccessToken, string? AccessSecret);

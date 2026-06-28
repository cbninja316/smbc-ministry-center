using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;
using SmbcStatusBoard.Api.Services;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/praisecharts")]
[Authorize]
public class PraiseChartsController(AppDbContext db, PraiseChartsService pc, IConfiguration config) : ControllerBase
{
    private bool CanWorship() =>
        User.IsInRole("SuperAdmin") ||
        (User.FindFirstValue("AllowedItemTypes") ?? "").Split(',').Contains("Worship", StringComparer.OrdinalIgnoreCase);

    // GET /api/praisecharts/status
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        if (!CanWorship()) return Forbid();
        var (ck, cs, at, asc) = await pc.GetCredentialsAsync();
        return Ok(new { connected = pc.IsConfigured(ck, cs, at, asc) });
    }

    // GET /api/praisecharts/library — proxy the user's purchased library
    [HttpGet("library")]
    public async Task<IActionResult> Library()
    {
        if (!CanWorship()) return Forbid();
        var (ck, cs, at, asc) = await pc.GetCredentialsAsync();
        if (!pc.IsConfigured(ck, cs, at, asc))
            return BadRequest(new { message = "PraiseCharts is not connected." });

        var result = await pc.GetLibraryAsync(ck!, cs!, at!, asc!);
        if (result == null) return StatusCode(502, new { message = "PraiseCharts API error." });
        return Ok(result);
    }

    // GET /api/praisecharts/search?q=...&page=1
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int page = 1)
    {
        if (!CanWorship()) return Forbid();
        if (string.IsNullOrWhiteSpace(q)) return BadRequest(new { message = "Query required." });
        var (ck, cs, at, asc) = await pc.GetCredentialsAsync();
        if (!pc.IsConfigured(ck, cs, at, asc))
            return BadRequest(new { message = "PraiseCharts is not connected." });

        var result = await pc.SearchSongsAsync(q, page, ck!, cs!, at!, asc!);
        if (result == null) return StatusCode(502, new { message = "PraiseCharts API error." });
        return Ok(result);
    }

    // GET /api/praisecharts/songs/{id}
    [HttpGet("songs/{id}")]
    public async Task<IActionResult> Song(string id)
    {
        if (!CanWorship()) return Forbid();
        var (ck, cs, at, asc) = await pc.GetCredentialsAsync();
        if (!pc.IsConfigured(ck, cs, at, asc))
            return BadRequest(new { message = "PraiseCharts is not connected." });

        var result = await pc.GetSongAsync(id, ck!, cs!, at!, asc!);
        if (result == null) return NotFound();
        return Ok(result);
    }

    // GET /api/praisecharts/buy-url?slug=... — returns the PraiseCharts URL to open in a new tab
    [HttpGet("buy-url")]
    public IActionResult BuyUrl([FromQuery] string? slug)
    {
        if (!CanWorship()) return Forbid();
        var baseUrl = string.IsNullOrEmpty(slug)
            ? "https://www.praisecharts.com/songs"
            : $"https://www.praisecharts.com/songs/details/{slug}";
        return Ok(new { url = baseUrl });
    }

    // POST /api/praisecharts/oauth/start — starts the 3-legged OAuth flow
    [HttpPost("oauth/start")]
    public async Task<IActionResult> OAuthStart()
    {
        if (!User.IsInRole("SuperAdmin")) return Forbid();
        var ck = (await db.AppSettings.FindAsync("PraiseCharts:ConsumerKey"))?.Value;
        var cs = (await db.AppSettings.FindAsync("PraiseCharts:ConsumerSecret"))?.Value;
        if (string.IsNullOrEmpty(ck) || string.IsNullOrEmpty(cs))
            return BadRequest(new { message = "Consumer Key and Secret must be saved first." });

        var frontendUrl = config["App:NextFrontendUrl"] ?? config["App:SiteUrl"] ?? config["App:FrontendUrl"] ?? "http://localhost:3000";
        var callbackUrl = $"{frontendUrl}/settings?pc_callback=1";
        var (requestToken, requestSecret) = await pc.GetRequestTokenAsync(ck, cs, callbackUrl);
        if (requestToken == null) return StatusCode(502, new { message = "Failed to get request token from PraiseCharts." });

        // Store request secret temporarily in AppSettings (keyed by token)
        await UpsertSetting($"PraiseCharts:RequestSecret:{requestToken}", requestSecret!);
        await db.SaveChangesAsync();

        return Ok(new { authorizeUrl = $"https://www.praisecharts.com/api/oauth/authorize?oauth_token={requestToken}" });
    }

    // POST /api/praisecharts/oauth/callback — exchanges verifier for access token
    [HttpPost("oauth/callback")]
    public async Task<IActionResult> OAuthCallback([FromBody] OAuthCallbackRequest req)
    {
        if (!User.IsInRole("SuperAdmin")) return Forbid();
        var ck = (await db.AppSettings.FindAsync("PraiseCharts:ConsumerKey"))?.Value;
        var cs = (await db.AppSettings.FindAsync("PraiseCharts:ConsumerSecret"))?.Value;
        var requestSecretEntry = await db.AppSettings.FindAsync($"PraiseCharts:RequestSecret:{req.OauthToken}");
        if (string.IsNullOrEmpty(ck) || string.IsNullOrEmpty(cs) || requestSecretEntry == null)
            return BadRequest(new { message = "Invalid OAuth state." });

        var (accessToken, accessSecret) = await pc.ExchangeForAccessTokenAsync(
            ck, cs, req.OauthToken, requestSecretEntry.Value, req.OauthVerifier);
        if (accessToken == null) return StatusCode(502, new { message = "Failed to exchange token." });

        await UpsertSetting("PraiseCharts:AccessToken", accessToken);
        await UpsertSetting("PraiseCharts:AccessSecret", accessSecret!);
        db.AppSettings.Remove(requestSecretEntry);
        await db.SaveChangesAsync();

        return Ok(new { message = "PraiseCharts account connected successfully." });
    }

    private async Task UpsertSetting(string key, string value)
    {
        var existing = await db.AppSettings.FindAsync(key);
        if (existing is null) db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        else existing.Value = value;
    }
}

public record OAuthCallbackRequest(string OauthToken, string OauthVerifier);

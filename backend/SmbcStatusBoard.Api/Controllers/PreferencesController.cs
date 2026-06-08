using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmbcStatusBoard.Api.Data;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PreferencesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        return Ok(new { data = user.PreferencesJson });
    }

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] PreferencesSaveRequest req)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        user.PreferencesJson = req.Data;
        await db.SaveChangesAsync();

        return Ok(new { message = "Preferences saved." });
    }
}

public record PreferencesSaveRequest(string Data);

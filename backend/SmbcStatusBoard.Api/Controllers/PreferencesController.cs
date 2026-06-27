using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;
using System.Security.Claims;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/preferences")]
[Authorize]
public class PreferencesController(AppDbContext db) : ControllerBase
{
    private int CurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key)
    {
        var pref = await db.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == CurrentUserId() && p.Key == key);
        return Ok(new { value = pref?.Value });
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Put(string key, [FromBody] PrefRequest req)
    {
        var userId = CurrentUserId();
        var pref = await db.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Key == key);
        if (pref == null)
        {
            pref = new UserPreference { UserId = userId, Key = key, Value = req.Value };
            db.UserPreferences.Add(pref);
        }
        else
        {
            pref.Value = req.Value;
        }
        await db.SaveChangesAsync();
        return Ok(new { value = pref.Value });
    }
}

public record PrefRequest(string Value);

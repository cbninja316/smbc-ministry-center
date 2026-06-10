using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.DTOs;
using SmbcStatusBoard.Api.Models;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/special-events")]
[Authorize]
public class SpecialEventController(AppDbContext db) : ControllerBase
{
    private bool CanManage()
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (role == "SuperAdmin") return true;
        var allowed = User.FindFirst("AllowedItemTypes")?.Value ?? "";
        return allowed.Split(',', StringSplitOptions.RemoveEmptyEntries).Contains("AssignVolunteers");
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!CanManage()) return Forbid();
        var events = await db.SpecialEvents
            .OrderBy(e => e.StartDate)
            .Select(e => new SpecialEventResponse
            {
                Id = e.Id,
                Label = e.Label,
                Description = e.Description,
                StartDate = e.StartDate.ToString("yyyy-MM-dd"),
                EndDate = e.EndDate.HasValue ? e.EndDate.Value.ToString("yyyy-MM-dd") : null,
                Recurrence = e.Recurrence.ToString()
            })
            .ToListAsync();
        return Ok(events);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSpecialEventRequest req)
    {
        if (!CanManage()) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Label))
            return BadRequest(new { message = "Label is required." });

        if (!Enum.TryParse<RecurrenceType>(req.Recurrence, out var recurrence))
            recurrence = RecurrenceType.None;

        var e = new SpecialEvent
        {
            Label = req.Label.Trim(),
            Description = req.Description?.Trim() ?? "",
            StartDate = req.StartDate.Date,
            EndDate = req.EndDate.HasValue ? req.EndDate.Value.Date : null,
            Recurrence = recurrence,
        };
        db.SpecialEvents.Add(e);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new SpecialEventResponse
        {
            Id = e.Id,
            Label = e.Label,
            Description = e.Description,
            StartDate = e.StartDate.ToString("yyyy-MM-dd"),
            EndDate = e.EndDate.HasValue ? e.EndDate.Value.ToString("yyyy-MM-dd") : null,
            Recurrence = e.Recurrence.ToString()
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!CanManage()) return Forbid();
        var e = await db.SpecialEvents.FindAsync(id);
        if (e is null) return NotFound();
        db.SpecialEvents.Remove(e);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

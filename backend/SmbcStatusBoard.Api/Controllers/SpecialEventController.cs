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

    private static SpecialEventResponse MapResponse(SpecialEvent e) => new()
    {
        Id = e.Id,
        Label = e.Label,
        Description = e.Description,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        EndDate = e.EndDate.HasValue ? e.EndDate.Value.ToString("yyyy-MM-dd") : null,
        Recurrence = e.Recurrence.ToString(),
        TimeSlots = e.TimeSlots.OrderBy(s => s.SortOrder).Select(s => new TimeSlotDto(s.Time, s.Label, s.SortOrder)).ToList(),
    };

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!CanManage()) return Forbid();
        var events = await db.SpecialEvents
            .Include(e => e.TimeSlots)
            .OrderBy(e => e.StartDate)
            .ToListAsync();
        return Ok(events.Select(MapResponse));
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
            TimeSlots = (req.TimeSlots ?? []).Select((s, i) => new SpecialEventTimeSlot
            {
                Time = s.Time,
                Label = s.Label,
                SortOrder = i,
            }).ToList(),
        };
        db.SpecialEvents.Add(e);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), MapResponse(e));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSpecialEventRequest req)
    {
        if (!CanManage()) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Label))
            return BadRequest(new { message = "Label is required." });

        var e = await db.SpecialEvents.Include(x => x.TimeSlots).FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();

        if (!Enum.TryParse<RecurrenceType>(req.Recurrence, out var recurrence))
            recurrence = RecurrenceType.None;

        e.Label = req.Label.Trim();
        e.Description = req.Description?.Trim() ?? "";
        e.StartDate = req.StartDate.Date;
        e.EndDate = req.EndDate.HasValue ? req.EndDate.Value.Date : null;
        e.Recurrence = recurrence;

        db.SpecialEventTimeSlots.RemoveRange(e.TimeSlots);
        e.TimeSlots = (req.TimeSlots ?? []).Select((s, i) => new SpecialEventTimeSlot
        {
            Time = s.Time,
            Label = s.Label,
            SortOrder = i,
        }).ToList();

        await db.SaveChangesAsync();
        return Ok(MapResponse(e));
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

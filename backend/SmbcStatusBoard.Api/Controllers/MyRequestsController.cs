using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.DTOs;
using SmbcStatusBoard.Api.Models;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/my-requests")]
[Authorize]
public class MyRequestsController(AppDbContext db) : ControllerBase
{
    private int GetUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private string GetRole() => User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? "";
    private string[] GetAllowedTypes() =>
        User.FindFirst("AllowedItemTypes")?.Value.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];

    // The requestable types (same set as the public submit form)
    private static readonly string[] RequestableTypes =
        ["ChurchEvent", "FacilityUse", "Benevolence", "Maintenance", "SecretaryRequest"];

    // For a normal Admin, returns types they cannot access on the dashboard (i.e., they can request these)
    // For a Member, returns all requestable types
    private string[] GetSubmittableTypes()
    {
        var role = GetRole();
        if (role == "Member") return RequestableTypes;
        if (role == "Admin")
        {
            var allowed = GetAllowedTypes();
            return RequestableTypes.Where(t => !allowed.Contains(t, StringComparer.OrdinalIgnoreCase)).ToArray();
        }
        return []; // SuperAdmin shouldn't use this endpoint
    }

    // GET /api/my-requests
    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        var role = GetRole();
        if (role == "SuperAdmin") return Forbid();

        var items = await db.Items
            .Where(i => i.SubmittedByUserId == userId)
            .OrderByDescending(i => i.SubmittedAt)
            .Select(i => new
            {
                i.Id,
                Type = i.Type.ToString(),
                i.Name,
                i.RequestedBy,
                i.Email,
                i.Description,
                i.Ministry,
                Urgency = i.Urgency.HasValue ? i.Urgency.ToString() : null,
                EventDate = i.EventDate.HasValue ? i.EventDate.Value.ToString("yyyy-MM-dd") : null,
                EventEndDate = i.EventEndDate.HasValue ? i.EventEndDate.Value.ToString("yyyy-MM-dd") : null,
                Status = i.Status.ToString(),
                SubmittedAt = i.SubmittedAt.ToString("yyyy-MM-dd"),
            })
            .ToListAsync();

        return Ok(items);
    }

    // POST /api/my-requests
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ItemRequest req)
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        var role = GetRole();
        if (role == "SuperAdmin") return Forbid();

        var submittable = GetSubmittableTypes();
        if (!submittable.Contains(req.Type.ToString(), StringComparer.OrdinalIgnoreCase))
            return Forbid();

        var item = new Item
        {
            Type = req.Type,
            Name = req.Name,
            EventDate = req.EventDate,
            EventEndDate = req.EventEndDate,
            Ministry = req.Ministry,
            Urgency = req.Urgency,
            RequestedBy = req.RequestedBy,
            Email = req.Email,
            Description = req.Description,
            Status = ItemStatus.ToDo,
            SortOrder = await db.Items.CountAsync(i => i.Status == ItemStatus.ToDo),
            SubmittedByUserId = userId,
            BenevolenceData = req.BenevolenceData != null ? JsonSerializer.Serialize(req.BenevolenceData) : null,
            ChurchEventData = req.ChurchEventData != null ? JsonSerializer.Serialize(req.ChurchEventData) : null,
        };

        db.Items.Add(item);
        await db.SaveChangesAsync();

        return Ok(new { item.Id, item.Name, Status = item.Status.ToString(), SubmittedAt = item.SubmittedAt.ToString("yyyy-MM-dd") });
    }

    // PUT /api/my-requests/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ItemRequest req)
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        var item = await db.Items.FindAsync(id);
        if (item is null) return NotFound();
        if (item.SubmittedByUserId != userId) return Forbid();
        if (item.Status != ItemStatus.ToDo)
            return BadRequest(new { message = "Only pending requests can be edited." });

        item.Name = req.Name;
        item.EventDate = req.EventDate;
        item.EventEndDate = req.EventEndDate;
        item.Ministry = req.Ministry;
        item.Urgency = req.Urgency;
        item.RequestedBy = req.RequestedBy;
        item.Email = req.Email;
        item.Description = req.Description;
        if (req.BenevolenceData != null) item.BenevolenceData = JsonSerializer.Serialize(req.BenevolenceData);
        if (req.ChurchEventData != null) item.ChurchEventData = JsonSerializer.Serialize(req.ChurchEventData);

        await db.SaveChangesAsync();
        return Ok(new { item.Id, item.Name, Status = item.Status.ToString() });
    }

    // DELETE /api/my-requests/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        var item = await db.Items.FindAsync(id);
        if (item is null) return NotFound();
        if (item.SubmittedByUserId != userId) return Forbid();
        if (item.Status != ItemStatus.ToDo)
            return BadRequest(new { message = "Only pending requests can be deleted." });

        db.Items.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

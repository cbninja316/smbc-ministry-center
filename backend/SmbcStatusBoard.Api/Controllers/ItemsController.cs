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
[Route("api/[controller]")]
[Authorize]
public class ItemsController(AppDbContext db) : ControllerBase
{
    private string[] GetAllowedTypes() =>
        User.FindFirst("AllowedItemTypes")?.Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];

    private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");

    private bool CanAccessType(ItemType type)
    {
        if (IsSuperAdmin()) return true;
        var allowed = GetAllowedTypes();
        return allowed.Contains(type.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        await AutoAdvanceStatusAsync();

        var query = db.Items.AsQueryable();

        if (!IsSuperAdmin())
        {
            var allowed = GetAllowedTypes()
                .Select(t => Enum.TryParse<ItemType>(t, true, out var parsed) ? (ItemType?)parsed : null)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .ToList();

            query = query.Where(i => allowed.Contains(i.Type));
        }

        var items = await query.OrderBy(i => i.Status).ThenBy(i => i.SortOrder).ToListAsync();
        return Ok(items);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        await AutoAdvanceStatusAsync();

        return Ok(new
        {
            events = await db.Items.CountAsync(i => i.Type == ItemType.ChurchEvent),
            facilityUses = await db.Items.CountAsync(i => i.Type == ItemType.FacilityUse),
            benevolence = await db.Items.CountAsync(i => i.Type == ItemType.Benevolence),
            maintenance = await db.Items.CountAsync(i => i.Type == ItemType.Maintenance)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ItemRequest req)
    {
        if (!CanAccessType(req.Type)) return Forbid();

        var item = new Item
        {
            Type = req.Type,
            Name = req.Name,
            EventDate = req.EventDate,
            Ministry = req.Ministry,
            Urgency = req.Urgency,
            RequestedBy = req.RequestedBy,
            Description = req.Description,
            Status = ItemStatus.ToDo,
            SortOrder = await db.Items.CountAsync(i => i.Status == ItemStatus.ToDo),
            BenevolenceData = req.BenevolenceData != null
                ? JsonSerializer.Serialize(req.BenevolenceData)
                : null,
            ChurchEventData = req.ChurchEventData != null
                ? JsonSerializer.Serialize(req.ChurchEventData)
                : null
        };

        db.Items.Add(item);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = item.Id }, item);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ItemRequest req)
    {
        var item = await db.Items.FindAsync(id);
        if (item is null) return NotFound();
        if (!CanAccessType(item.Type)) return Forbid();

        item.Name = req.Name;
        item.EventDate = req.EventDate;
        item.Ministry = req.Ministry;
        item.Urgency = req.Urgency;
        item.RequestedBy = req.RequestedBy;
        item.Description = req.Description;
        item.BenevolenceData = req.BenevolenceData != null
            ? JsonSerializer.Serialize(req.BenevolenceData)
            : item.BenevolenceData;
        item.ChurchEventData = req.ChurchEventData != null
            ? JsonSerializer.Serialize(req.ChurchEventData)
            : item.ChurchEventData;

        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await db.Items.FindAsync(id);
        if (item is null) return NotFound();
        if (!CanAccessType(item.Type)) return Forbid();

        db.Items.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] List<ItemReorderRequest> updates)
    {
        foreach (var update in updates)
        {
            var item = await db.Items.FindAsync(update.Id);
            if (item is null || !CanAccessType(item.Type)) continue;
            item.Status = update.Status;
            item.SortOrder = update.SortOrder;
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    private async Task AutoAdvanceStatusAsync()
    {
        var now = DateTime.UtcNow.Date;
        var autoTypes = new[] { ItemType.ChurchEvent, ItemType.FacilityUse };

        var items = await db.Items
            .Where(i => autoTypes.Contains(i.Type) && i.EventDate.HasValue && i.Status != ItemStatus.Done)
            .ToListAsync();

        foreach (var item in items)
        {
            var eventDate = item.EventDate!.Value.Date;
            if (eventDate <= now && item.Status == ItemStatus.ToDo)
                item.Status = ItemStatus.InProgress;
            if (eventDate < now && item.Status == ItemStatus.InProgress)
                item.Status = ItemStatus.Done;
        }

        await db.SaveChangesAsync();
    }
}

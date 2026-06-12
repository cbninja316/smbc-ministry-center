using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.DTOs;
using SmbcStatusBoard.Api.Models;
using SmbcStatusBoard.Api.Services;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ItemsController(AppDbContext db, FileStorageService storage, EmailService email) : ControllerBase
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
            maintenance = await db.Items.CountAsync(i => i.Type == ItemType.Maintenance),
            secretaryRequests = await db.Items.CountAsync(i => i.Type == ItemType.SecretaryRequest)
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
            EventEndDate = req.EventEndDate,
            Ministry = req.Ministry,
            Urgency = req.Urgency,
            RequestedBy = req.RequestedBy,
            Email = req.Email,
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
        item.EventEndDate = req.EventEndDate;
        item.Ministry = req.Ministry;
        item.Urgency = req.Urgency;
        item.RequestedBy = req.RequestedBy;
        item.Email = req.Email;
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

        // Delete event photos (DB records + disk files) when a ChurchEvent is removed
        if (item.Type == ItemType.ChurchEvent)
        {
            var photos = await db.EventPhotos.Where(p => p.ItemId == id).ToListAsync();
            db.EventPhotos.RemoveRange(photos);
            await db.SaveChangesAsync();
            try { storage.DeleteEventPhotosFolder(id); } catch { /* best-effort; don't block item delete */ }
        }

        db.Items.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] List<ItemReorderRequest> updates)
    {
        var completedItems = new List<Item>();

        foreach (var update in updates)
        {
            var item = await db.Items.FindAsync(update.Id);
            if (item is null || !CanAccessType(item.Type)) continue;

            // Track items that just flipped to Done so we can email after save
            if (update.Status == ItemStatus.Done && item.Status != ItemStatus.Done
                && !string.IsNullOrWhiteSpace(item.Email))
            {
                completedItems.Add(item);
            }

            item.Status = update.Status;
            item.SortOrder = update.SortOrder;
        }

        await db.SaveChangesAsync();

        // Send completion emails (fire-and-forget; don't let email errors fail the response)
        foreach (var item in completedItems)
        {
            try { await SendCompletionEmailAsync(item); }
            catch { /* best-effort */ }
        }

        return Ok();
    }

    private static readonly Dictionary<ItemType, string> TypeLabels = new()
    {
        [ItemType.ChurchEvent]        = "Church Event",
        [ItemType.FacilityUse]        = "Facility Use Request",
        [ItemType.Benevolence]        = "Benevolence Request",
        [ItemType.Maintenance]        = "Maintenance Request",
        [ItemType.SecretaryRequest]   = "Secretary Request",
    };

    private async Task SendCompletionEmailAsync(Item item)
    {
        var typeLabel = TypeLabels.GetValueOrDefault(item.Type, item.Type.ToString());
        var details = new List<(string Label, string Value)>();

        // Fields common to all types
        if (!string.IsNullOrWhiteSpace(item.Name))
            details.Add(("Name / Title", item.Name));
        if (!string.IsNullOrWhiteSpace(item.RequestedBy))
            details.Add(("Submitted By", item.RequestedBy));
        if (!string.IsNullOrWhiteSpace(item.Ministry))
            details.Add(("Ministry", item.Ministry));
        if (item.EventDate.HasValue)
            details.Add(("Date", item.EventDate.Value.ToString("MMMM d, yyyy")));
        if (!string.IsNullOrWhiteSpace(item.Description))
            details.Add(("Description", item.Description));
        details.Add(("Submitted On", item.SubmittedAt.ToString("MMMM d, yyyy")));

        // Type-specific extras
        if (item.Type == ItemType.ChurchEvent && item.ChurchEventDetails is { } ce)
        {
            if (!string.IsNullOrWhiteSpace(ce.StartTime))
                details.Add(("Start Time", ce.StartTime));
            if (!string.IsNullOrWhiteSpace(ce.EndTime))
                details.Add(("End Time", ce.EndTime));
            if (!string.IsNullOrWhiteSpace(ce.Location))
                details.Add(("Location", ce.Location));
        }

        if (item.Type == ItemType.Benevolence && item.BenevolenceDetails is { } ben)
        {
            if (ben.AmountRequested.HasValue)
                details.Add(("Amount Requested", $"${ben.AmountRequested:F2}"));
            if (!string.IsNullOrWhiteSpace(ben.Determination))
                details.Add(("Determination", ben.Determination switch
                {
                    "ApprovedFull" => "Approved in Full",
                    "ApprovedPart" => "Approved in Part",
                    "NotApproved"  => "Not Approved",
                    _              => ben.Determination
                }));
        }

        await email.SendItemCompletedAsync(item.Email!, item.RequestedBy, typeLabel, details);
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
            var startDate = item.EventDate!.Value.Date;
            var endDate = (item.EventEndDate ?? item.EventDate)!.Value.Date;
            if (startDate <= now && item.Status == ItemStatus.ToDo)
                item.Status = ItemStatus.InProgress;
            if (endDate < now && item.Status == ItemStatus.InProgress)
                item.Status = ItemStatus.Done;
        }

        await db.SaveChangesAsync();
    }
}

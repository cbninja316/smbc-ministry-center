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

    // Any authenticated user can see home-page events regardless of AllowedItemTypes
    [HttpGet("home-events")]
    public async Task<IActionResult> GetHomeEvents()
    {
        var items = await db.Items
            .Where(i => i.Type == ItemType.ChurchEvent && i.Status != ItemStatus.ToDo)
            .OrderBy(i => i.EventDate)
            .ToListAsync();

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
        var completedItems = new List<(Item Item, string? Note)>();

        foreach (var update in updates)
        {
            var item = await db.Items.FindAsync(update.Id);
            if (item is null || !CanAccessType(item.Type)) continue;

            // Track items that just flipped to Done so we can email after save
            if (update.Status == ItemStatus.Done && item.Status != ItemStatus.Done
                && !string.IsNullOrWhiteSpace(item.Email))
            {
                completedItems.Add((item, update.CompletionNote));
            }

            item.Status = update.Status;
            item.SortOrder = update.SortOrder;
            if (update.Status == ItemStatus.Done && !string.IsNullOrWhiteSpace(update.CompletionNote))
                item.CompletionNote = update.CompletionNote.Trim();
        }

        await db.SaveChangesAsync();

        // Send completion emails (fire-and-forget; don't let email errors fail the response)
        foreach (var (item, note) in completedItems)
        {
            try { await SendCompletionEmailAsync(item, note); }
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

    private async Task SendCompletionEmailAsync(Item item, string? note = null)
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

        await email.SendItemCompletedAsync(item.Email!, item.RequestedBy, typeLabel, details, note);
    }

    // POST /api/items/{id}/register
    [HttpPost("{id}/register")]
    [Authorize]
    public async Task<IActionResult> Register(int id, [FromBody] EventRegisterRequest req)
    {
        var item = await db.Items.FindAsync(id);
        if (item == null) return NotFound();

        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var me = await db.Users.Include(u => u.Children).Include(u => u.Spouse).ThenInclude(s => s!.Children)
            .FirstOrDefaultAsync(u => u.Id == uid);
        if (me == null) return NotFound();

        var familyUserIds = new HashSet<int> { uid };
        if (me.SpouseUserId.HasValue) familyUserIds.Add(me.SpouseUserId.Value);

        var familyChildIds = me.Children.Select(c => c.Id)
            .Concat(me.Spouse?.Children.Select(c => c.Id) ?? [])
            .ToHashSet();

        foreach (var userId in req.UserIds ?? [])
        {
            if (!familyUserIds.Contains(userId)) return Forbid();
            var already = await db.EventRegistrations.AnyAsync(r => r.ItemId == id && r.UserId == userId);
            if (!already) db.EventRegistrations.Add(new EventRegistration { ItemId = id, UserId = userId });
        }

        foreach (var childId in req.ChildIds ?? [])
        {
            if (!familyChildIds.Contains(childId)) return Forbid();
            var already = await db.EventRegistrations.AnyAsync(r => r.ItemId == id && r.ChildId == childId);
            if (!already) db.EventRegistrations.Add(new EventRegistration { ItemId = id, ChildId = childId });
        }

        await db.SaveChangesAsync();

        // Send confirmation email to member and spouse (if they have email)
        try
        {
            var details = item.ChurchEventDetails;

            // Build list of registered names for the email body
            var registeredUsers = req.UserIds?.Length > 0
                ? await db.Users.Where(u => req.UserIds.Contains(u.Id)).ToListAsync()
                : [];
            var registeredChildren = req.ChildIds?.Length > 0
                ? await db.Children.Where(c => req.ChildIds.Contains(c.Id)).ToListAsync()
                : [];

            var registeredNames = registeredUsers.Select(u => $"{u.FirstName} {u.LastName}".Trim())
                .Concat(registeredChildren.Select(c => $"{c.FirstName} {c.LastName}".Trim()))
                .ToList();

            if (registeredNames.Count > 0)
            {
                // Collect email recipients: the registering user + their spouse
                var emailRecipients = new List<(string Email, string Name)>();
                if (!string.IsNullOrWhiteSpace(me.Email))
                    emailRecipients.Add((me.Email, $"{me.FirstName} {me.LastName}".Trim()));
                if (me.Spouse != null && !string.IsNullOrWhiteSpace(me.Spouse.Email))
                    emailRecipients.Add((me.Spouse.Email, $"{me.Spouse.FirstName} {me.Spouse.LastName}".Trim()));

                // Format date range
                string? dateRange = null;
                if (item.EventDate.HasValue)
                {
                    dateRange = item.EventEndDate.HasValue && item.EventEndDate != item.EventDate
                        ? $"{item.EventDate.Value:MMM d, yyyy} – {item.EventEndDate.Value:MMM d, yyyy}"
                        : item.EventDate.Value.ToString("MMM d, yyyy");
                    if (!string.IsNullOrWhiteSpace(details?.StartTime))
                        dateRange += $" · {FormatTime12Hr(details.StartTime)}";
                    if (!string.IsNullOrWhiteSpace(details?.EndTime))
                        dateRange += $" – {FormatTime12Hr(details.EndTime)}";
                }

                await email.SendEventRegistrationAsync(
                    emailRecipients,
                    item.Name,
                    dateRange,
                    details?.Location,
                    registeredNames);
            }
        }
        catch { /* email failure should not block the registration response */ }

        return Ok(new { message = "Registered." });
    }

    // DELETE /api/items/{id}/register
    [HttpDelete("{id}/register")]
    [Authorize]
    public async Task<IActionResult> Unregister(int id, [FromBody] EventRegisterRequest req)
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var me = await db.Users.Include(u => u.Children).Include(u => u.Spouse).ThenInclude(s => s!.Children)
            .FirstOrDefaultAsync(u => u.Id == uid);
        if (me == null) return NotFound();

        var familyUserIds = new HashSet<int> { uid };
        if (me.SpouseUserId.HasValue) familyUserIds.Add(me.SpouseUserId.Value);
        var familyChildIds = me.Children.Select(c => c.Id)
            .Concat(me.Spouse?.Children.Select(c => c.Id) ?? [])
            .ToHashSet();

        foreach (var userId in req.UserIds ?? [])
        {
            if (!familyUserIds.Contains(userId)) continue;
            var reg = await db.EventRegistrations.FirstOrDefaultAsync(r => r.ItemId == id && r.UserId == userId);
            if (reg != null) db.EventRegistrations.Remove(reg);
        }
        foreach (var childId in req.ChildIds ?? [])
        {
            if (!familyChildIds.Contains(childId)) continue;
            var reg = await db.EventRegistrations.FirstOrDefaultAsync(r => r.ItemId == id && r.ChildId == childId);
            if (reg != null) db.EventRegistrations.Remove(reg);
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/items/{id}/registrations (admin)
    [HttpGet("{id}/registrations")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetRegistrations(int id)
    {
        var regs = await db.EventRegistrations
            .Where(r => r.ItemId == id)
            .Include(r => r.User)
            .Include(r => r.Child)
            .OrderBy(r => r.RegisteredAt)
            .ToListAsync();

        return Ok(regs.Select(r => r.UserId.HasValue
            ? new {
                type = "user",
                id = r.UserId.Value,
                firstName = r.User!.FirstName,
                lastName = r.User.LastName,
                username = r.User.Username,
                email = r.User.Email,
                birthDate = r.User.BirthDate?.ToString("yyyy-MM-dd"),
                registeredAt = r.RegisteredAt,
              }
            : new {
                type = "child",
                id = r.ChildId!.Value,
                firstName = r.Child!.FirstName,
                lastName = r.Child.LastName,
                username = (string?)null,
                email = (string?)null,
                birthDate = r.Child.BirthDate?.ToString("yyyy-MM-dd"),
                registeredAt = r.RegisteredAt,
              }
        ));
    }

    // GET /api/items/{id}/my-registrations
    [HttpGet("{id}/my-registrations")]
    [Authorize]
    public async Task<IActionResult> GetMyRegistrations(int id)
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var me = await db.Users.Include(u => u.Children).Include(u => u.Spouse).ThenInclude(s => s!.Children)
            .FirstOrDefaultAsync(u => u.Id == uid);
        if (me == null) return NotFound();

        var familyUserIds = new HashSet<int> { uid };
        if (me.SpouseUserId.HasValue) familyUserIds.Add(me.SpouseUserId.Value);
        var familyChildIds = me.Children.Select(c => c.Id)
            .Concat(me.Spouse?.Children.Select(c => c.Id) ?? [])
            .ToHashSet();

        var regs = await db.EventRegistrations
            .Where(r => r.ItemId == id && (
                (r.UserId.HasValue && familyUserIds.Contains(r.UserId.Value)) ||
                (r.ChildId.HasValue && familyChildIds.Contains(r.ChildId.Value))
            ))
            .ToListAsync();

        return Ok(new {
            userIds = regs.Where(r => r.UserId.HasValue).Select(r => r.UserId!.Value).ToList(),
            childIds = regs.Where(r => r.ChildId.HasValue).Select(r => r.ChildId!.Value).ToList(),
        });
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

    private static string FormatTime12Hr(string? time)
    {
        if (string.IsNullOrWhiteSpace(time)) return time ?? "";
        if (TimeSpan.TryParse(time, out var ts))
        {
            var hour = ts.Hours % 12 == 0 ? 12 : ts.Hours % 12;
            var suffix = ts.Hours < 12 ? "AM" : "PM";
            return ts.Minutes == 0 ? $"{hour} {suffix}" : $"{hour}:{ts.Minutes:D2} {suffix}";
        }
        return time;
    }
}

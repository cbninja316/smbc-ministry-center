using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.DTOs;
using SmbcStatusBoard.Api.Models;
using SmbcStatusBoard.Api.Services;
using System.Text.Json;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController(AppDbContext db, FileStorageService storage, EmailService emailService) : ControllerBase
{
    // POST /api/public/items — anonymous item submission
    [HttpPost("items")]
    public async Task<IActionResult> SubmitItem([FromBody] ItemRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.RequestedBy))
            return BadRequest(new { message = "Requested By is required." });

        if (string.IsNullOrWhiteSpace(req.Description))
            return BadRequest(new { message = "Description is required." });

        if (req.Type == ItemType.ChurchEvent && string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Event Name is required." });

        if (req.Type == ItemType.FacilityUse && string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Event Name is required." });

        if (req.Type == ItemType.Maintenance && string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Title is required." });

        var maxSort = await db.Items
            .Where(i => i.Status == ItemStatus.ToDo)
            .MaxAsync(i => (int?)i.SortOrder) ?? 0;

        var item = new Item
        {
            Type        = req.Type,
            Name        = req.Name ?? "",
            Ministry    = req.Ministry,
            RequestedBy = req.RequestedBy,
            Email       = req.Email,
            EventDate   = req.EventDate,
            Urgency     = req.Urgency,
            Description = req.Description,
            Status      = ItemStatus.ToDo,
            SortOrder   = maxSort + 1,
            SubmittedAt = DateTime.UtcNow,
        };

        if (req.BenevolenceData != null)
            item.BenevolenceData = JsonSerializer.Serialize(req.BenevolenceData);

        if (req.ChurchEventData != null)
            item.ChurchEventData = JsonSerializer.Serialize(req.ChurchEventData);

        db.Items.Add(item);
        await db.SaveChangesAsync();

        // ── Notify users who have access to this item type ──────────────────
        try
        {
            var typeString = req.Type.ToString();
            var recipients = await db.Users
                .Where(u => u.IsActive && (
                    u.Role == UserRole.SuperAdmin ||
                    u.AllowedItemTypes.Contains(typeString)))
                .Select(u => new { u.Email, u.Username })
                .ToListAsync();

            var typeLabel = req.Type switch
            {
                ItemType.ChurchEvent      => "Church Event",
                ItemType.FacilityUse      => "Facility Use Request",
                ItemType.Benevolence      => "Benevolence Request",
                ItemType.Maintenance      => "Maintenance Request",
                ItemType.SecretaryRequest => "Secretary Request",
                _                         => req.Type.ToString()
            };

            var details = req.Type switch
            {
                ItemType.SecretaryRequest => new List<(string, string)>
                {
                    ("Requested By", req.RequestedBy),
                    ("Email",        req.Email ?? ""),
                    ("Description",  req.Description),
                    ("Submitted",    DateTime.UtcNow.ToString("MMMM d, yyyy h:mm tt") + " UTC"),
                },
                ItemType.Maintenance => new List<(string, string)>
                {
                    ("Title",        req.Name ?? ""),
                    ("Requested By", req.RequestedBy),
                    ("Urgency",      req.Urgency?.ToString() ?? ""),
                    ("Date Needed",  req.EventDate?.ToString("MMMM d, yyyy") ?? ""),
                    ("Details",      req.Description),
                    ("Submitted",    DateTime.UtcNow.ToString("MMMM d, yyyy h:mm tt") + " UTC"),
                },
                ItemType.FacilityUse => new List<(string, string)>
                {
                    ("Event Name",   req.Name ?? ""),
                    ("Requested By", req.RequestedBy),
                    ("Event Date",   req.EventDate?.ToString("MMMM d, yyyy") ?? ""),
                    ("Description",  req.Description),
                    ("Submitted",    DateTime.UtcNow.ToString("MMMM d, yyyy h:mm tt") + " UTC"),
                },
                ItemType.ChurchEvent => new List<(string, string)>
                {
                    ("Event Name",   req.Name ?? ""),
                    ("Ministry",     req.Ministry ?? ""),
                    ("Contact",      req.RequestedBy),
                    ("Event Date",   req.EventDate?.ToString("MMMM d, yyyy") ?? ""),
                    ("Event Time",   req.ChurchEventData?.EventTime ?? ""),
                    ("Location",     req.ChurchEventData?.Location ?? ""),
                    ("Cost",         req.ChurchEventData?.Cost?.ToString("C") ?? ""),
                    ("Description",  req.Description),
                    ("Submitted",    DateTime.UtcNow.ToString("MMMM d, yyyy h:mm tt") + " UTC"),
                },
                ItemType.Benevolence => new List<(string, string)>
                {
                    ("Applicant",    req.RequestedBy),
                    ("Phone",        req.BenevolenceData?.Phone ?? ""),
                    ("Address",      string.Join(", ", new[] {
                                         req.BenevolenceData?.StreetAddress,
                                         req.BenevolenceData?.City,
                                         req.BenevolenceData?.State,
                                         req.BenevolenceData?.ZipCode
                                     }.Where(s => !string.IsNullOrWhiteSpace(s)))),
                    ("Amount Requested", req.BenevolenceData?.AmountRequested?.ToString("C") ?? ""),
                    ("Date Needed",  req.BenevolenceData?.DateNeeded ?? ""),
                    ("Relationship", req.BenevolenceData?.RelationshipToChurch ?? ""),
                    ("Reason",       req.Description),
                    ("Submitted",    DateTime.UtcNow.ToString("MMMM d, yyyy h:mm tt") + " UTC"),
                },
                _ => new List<(string, string)>
                {
                    ("Requested By", req.RequestedBy),
                    ("Description",  req.Description),
                    ("Submitted",    DateTime.UtcNow.ToString("MMMM d, yyyy h:mm tt") + " UTC"),
                }
            };

            await emailService.SendNewRequestAsync(
                recipients.Select(r => (r.Email, r.Username)).ToList(),
                typeLabel,
                details);
        }
        catch (Exception ex)
        {
            // Don't fail the submission if email sending errors
            Console.WriteLine($"[Email] Failed to send new-request notification: {ex.Message}");
        }

        return Ok(new { message = "Submitted successfully." });
    }

    // POST /api/public/receipts — anonymous receipt submission
    [HttpPost("receipts")]
    public async Task<IActionResult> SubmitReceipt([FromForm] ReceiptUploadRequest req, IFormFile? image)
    {
        if (image is null || image.Length == 0)
            return BadRequest(new { message = "Receipt image is required." });

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "application/pdf" };
        if (!allowedTypes.Contains(image.ContentType))
            return BadRequest(new { message = "Only JPEG, PNG, WebP, or PDF files are allowed." });

        using var stream = image.OpenReadStream();
        var fileName = await storage.SaveReceiptAsync(stream, image.FileName);

        var receipt = new Receipt
        {
            Date          = req.Date,
            Ministry      = req.Ministry,
            Description   = req.Description,
            Amount        = req.Amount,
            SubmittedBy   = req.SubmittedBy,
            OneDriveFileId = fileName
        };

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync();

        return Ok(new { message = "Receipt submitted successfully." });
    }
}

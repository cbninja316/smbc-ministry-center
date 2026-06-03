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
public class PublicController(AppDbContext db, FileStorageService storage) : ControllerBase
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

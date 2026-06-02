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
public class ReceiptsController(AppDbContext db, FileStorageService storage) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var receipts = await db.Receipts
            .OrderByDescending(r => r.Date)
            .Select(r => new ReceiptResponse(r.Id, r.Date, r.Ministry, r.Description, r.Amount, r.SubmittedBy, r.IsDone))
            .ToListAsync();

        return Ok(receipts);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] ReceiptUploadRequest req, IFormFile image)
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
            Date = req.Date,
            Ministry = req.Ministry,
            Description = req.Description,
            Amount = req.Amount,
            SubmittedBy = req.SubmittedBy,
            OneDriveFileId = fileName
        };

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = receipt.Id },
            new ReceiptResponse(receipt.Id, receipt.Date, receipt.Ministry, receipt.Description, receipt.Amount, receipt.SubmittedBy, false));
    }

    [HttpPatch("{id}/done")]
    public async Task<IActionResult> MarkDone(int id)
    {
        var receipt = await db.Receipts.FindAsync(id);
        if (receipt is null) return NotFound();
        receipt.IsDone = true;
        await db.SaveChangesAsync();
        return Ok(new { message = "Receipt marked as done." });
    }

    [HttpGet("{id}/image")]
    public async Task<IActionResult> GetImage(int id)
    {
        var receipt = await db.Receipts.FindAsync(id);
        if (receipt is null) return NotFound();

        var stream = storage.GetReceiptStream(receipt.OneDriveFileId);
        var contentType = storage.GetContentType(receipt.OneDriveFileId);
        return File(stream, contentType);
    }
}

public record ReceiptUploadRequest(DateTime Date, string Ministry, string Description, decimal Amount, string SubmittedBy);

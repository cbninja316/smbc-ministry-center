using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;
using SmbcStatusBoard.Api.Services;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/event-photos")]
[Authorize]
public class EventPhotosController(AppDbContext db, FileStorageService storage) : ControllerBase
{
    private static readonly string[] AllowedImageTypes =
        ["image/jpeg", "image/png", "image/webp", "image/gif", "image/heic"];

    /// <summary>Returns all event-photo groups (events that have at least 1 photo).</summary>
    [HttpGet]
    public async Task<IActionResult> GetGroups()
    {
        var groups = await db.EventPhotos
            .GroupBy(p => p.ItemId)
            .Select(g => new
            {
                ItemId    = g.Key,
                PhotoCount = g.Count(),
                Latest    = g.Max(p => p.UploadedAt)
            })
            .ToListAsync();

        var itemIds = groups.Select(g => g.ItemId).ToList();
        var items   = await db.Items
            .Where(i => itemIds.Contains(i.Id))
            .ToListAsync();

        var result = groups
            .Select(g =>
            {
                var item = items.FirstOrDefault(i => i.Id == g.ItemId);
                return new
                {
                    itemId     = g.ItemId,
                    eventName  = item?.Name ?? "Unknown Event",
                    eventDate  = item?.EventDate,
                    photoCount = g.PhotoCount,
                    latestUpload = g.Latest
                };
            })
            .OrderByDescending(g => g.latestUpload);

        return Ok(result);
    }

    /// <summary>Returns all ChurchEvent items (for populating the "tag" dropdown).</summary>
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents()
    {
        var events = await db.Items
            .Where(i => i.Type == ItemType.ChurchEvent)
            .OrderByDescending(i => i.EventDate)
            .Select(i => new { i.Id, i.Name, i.EventDate })
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>Returns individual photo records for one event.</summary>
    [HttpGet("{itemId}/photos")]
    public async Task<IActionResult> GetPhotos(int itemId)
    {
        var photos = await db.EventPhotos
            .Where(p => p.ItemId == itemId)
            .OrderBy(p => p.UploadedAt)
            .Select(p => new { p.Id, p.ItemId, p.FileName, p.UploadedAt })
            .ToListAsync();

        return Ok(photos);
    }

    /// <summary>Uploads one or more photos and attaches them to an event.</summary>
    [HttpPost("{itemId}/photos")]
    public async Task<IActionResult> Upload(int itemId, [FromForm] IFormFileCollection photos)
    {
        var item = await db.Items.FindAsync(itemId);
        if (item is null) return NotFound(new { message = "Event not found." });

        if (photos == null || photos.Count == 0)
            return BadRequest(new { message = "No photos provided." });

        var saved = new List<object>();

        foreach (var photo in photos)
        {
            if (photo.Length == 0) continue;

            if (!AllowedImageTypes.Contains(photo.ContentType.ToLower()))
                continue; // skip non-image files silently

            using var stream = photo.OpenReadStream();
            var fileName = await storage.SaveEventPhotoAsync(itemId, stream, photo.FileName);

            var ep = new EventPhoto { ItemId = itemId, FileName = fileName };
            db.EventPhotos.Add(ep);
            await db.SaveChangesAsync();

            saved.Add(new { ep.Id, ep.ItemId, ep.FileName, ep.UploadedAt });
        }

        return Ok(saved);
    }

    /// <summary>Deletes a single photo record and its file on disk.</summary>
    [HttpDelete("photos/{photoId}")]
    public async Task<IActionResult> DeletePhoto(int photoId)
    {
        var photo = await db.EventPhotos.FindAsync(photoId);
        if (photo is null) return NotFound();

        storage.DeleteEventPhoto(photo.ItemId, photo.FileName);
        db.EventPhotos.Remove(photo);
        await db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>Serves the image file — AllowAnonymous so img tags work without JS auth headers.</summary>
    [HttpGet("{itemId}/photos/{photoId}/image")]
    [AllowAnonymous]
    public async Task<IActionResult> GetImage(int itemId, int photoId)
    {
        var photo = await db.EventPhotos.FindAsync(photoId);
        if (photo is null || photo.ItemId != itemId) return NotFound();

        try
        {
            var stream      = storage.GetEventPhotoStream(itemId, photo.FileName);
            var contentType = storage.GetContentType(photo.FileName);
            return File(stream, contentType);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }
}

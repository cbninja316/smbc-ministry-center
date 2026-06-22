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

    /// <summary>Returns all event-photo groups (events with at least 1 non-hero photo).</summary>
    [HttpGet]
    public async Task<IActionResult> GetGroups()
    {
        var groups = await db.EventPhotos
            .Where(p => !p.IsHeroImage)
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

    /// <summary>Returns individual non-hero photo records for one event.</summary>
    [HttpGet("{itemId}/photos")]
    public async Task<IActionResult> GetPhotos(int itemId)
    {
        var photos = await db.EventPhotos
            .Where(p => p.ItemId == itemId && !p.IsHeroImage)
            .OrderBy(p => p.UploadedAt)
            .Select(p => new { p.Id, p.ItemId, p.FileName, p.UploadedAt })
            .ToListAsync();

        return Ok(photos);
    }

    /// <summary>Uploads one or more non-hero photos and attaches them to an event.</summary>
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
                continue;

            using var stream = photo.OpenReadStream();
            var fileName = await storage.SaveEventPhotoAsync(itemId, stream, photo.FileName);

            var ep = new EventPhoto { ItemId = itemId, FileName = fileName };
            db.EventPhotos.Add(ep);
            await db.SaveChangesAsync();

            saved.Add(new { ep.Id, ep.ItemId, ep.FileName, ep.UploadedAt });
        }

        return Ok(saved);
    }

    /// <summary>Deletes a single non-hero photo record and its file.</summary>
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

    /// <summary>Serves a non-hero image file.</summary>
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

    // ── Hero Image endpoints ─────────────────────────────────────────────────

    /// <summary>Returns the hero image record for an event, or 404 if none.</summary>
    [HttpGet("{itemId}/hero")]
    public async Task<IActionResult> GetHero(int itemId)
    {
        var hero = await db.EventPhotos
            .Where(p => p.ItemId == itemId && p.IsHeroImage)
            .Select(p => new { p.Id, p.ItemId, p.FileName, p.UploadedAt })
            .FirstOrDefaultAsync();

        if (hero is null) return NotFound();
        return Ok(hero);
    }

    /// <summary>Uploads a hero image, replacing any existing one.</summary>
    [HttpPost("{itemId}/hero")]
    public async Task<IActionResult> UploadHero(int itemId, [FromForm] IFormFile photo)
    {
        var item = await db.Items.FindAsync(itemId);
        if (item is null) return NotFound(new { message = "Event not found." });

        if (photo is null || photo.Length == 0)
            return BadRequest(new { message = "No photo provided." });

        if (!AllowedImageTypes.Contains(photo.ContentType.ToLower()))
            return BadRequest(new { message = "Unsupported image type." });

        // Delete existing hero if any
        var existing = await db.EventPhotos
            .Where(p => p.ItemId == itemId && p.IsHeroImage)
            .ToListAsync();

        foreach (var old in existing)
        {
            storage.DeleteEventPhoto(itemId, old.FileName);
            db.EventPhotos.Remove(old);
        }

        using var stream = photo.OpenReadStream();
        var fileName = await storage.SaveEventPhotoAsync(itemId, stream, photo.FileName);

        var ep = new EventPhoto { ItemId = itemId, FileName = fileName, IsHeroImage = true };
        db.EventPhotos.Add(ep);
        await db.SaveChangesAsync();

        return Ok(new { ep.Id, ep.ItemId, ep.FileName, ep.UploadedAt });
    }

    /// <summary>Deletes the hero image for an event.</summary>
    [HttpDelete("{itemId}/hero")]
    public async Task<IActionResult> DeleteHero(int itemId)
    {
        var hero = await db.EventPhotos
            .Where(p => p.ItemId == itemId && p.IsHeroImage)
            .FirstOrDefaultAsync();

        if (hero is null) return NotFound();

        storage.DeleteEventPhoto(itemId, hero.FileName);
        db.EventPhotos.Remove(hero);
        await db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>Serves the hero image — AllowAnonymous so img tags work without JS auth headers.</summary>
    [HttpGet("{itemId}/hero/image")]
    [AllowAnonymous]
    public async Task<IActionResult> GetHeroImage(int itemId)
    {
        var hero = await db.EventPhotos
            .Where(p => p.ItemId == itemId && p.IsHeroImage)
            .FirstOrDefaultAsync();

        if (hero is null) return NotFound();

        try
        {
            var stream      = storage.GetEventPhotoStream(itemId, hero.FileName);
            var contentType = storage.GetContentType(hero.FileName);
            return File(stream, contentType);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }
}

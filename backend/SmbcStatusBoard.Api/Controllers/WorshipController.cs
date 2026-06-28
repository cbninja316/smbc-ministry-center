using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/worship")]
[Authorize]
public class WorshipController(AppDbContext db, IConfiguration config) : ControllerBase
{
    private bool CanWorship() =>
        User.IsInRole("SuperAdmin") ||
        (User.FindFirstValue("AllowedItemTypes") ?? "").Split(',').Contains("Worship", StringComparer.OrdinalIgnoreCase);

    private string UploadPath =>
        config["Storage:WorshipFilesPath"] ?? Path.Combine(AppContext.BaseDirectory, "worship-files");

    // ════════════════════════════════════════════════════════════════════════
    // LIBRARY — Songs
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("songs")]
    public async Task<IActionResult> GetSongs()
    {
        if (!CanWorship()) return Forbid();
        var songs = await db.WorshipSongs.OrderBy(s => s.Title).ToListAsync();
        return Ok(songs.Select(MapSong));
    }

    [HttpGet("songs/{id}")]
    public async Task<IActionResult> GetSong(int id)
    {
        if (!CanWorship()) return Forbid();
        var s = await db.WorshipSongs.FindAsync(id);
        if (s == null) return NotFound();
        return Ok(MapSong(s));
    }

    [HttpPost("songs")]
    public async Task<IActionResult> CreateSong([FromBody] SongPayload req)
    {
        if (!CanWorship()) return Forbid();
        var song = new WorshipSong
        {
            Title = req.Title.Trim(),
            Artist = req.Artist?.Trim() ?? "",
            DurationSeconds = req.DurationSeconds,
            PraiseChartsId = req.PraiseChartsId,
            PraiseChartsSlug = req.PraiseChartsSlug,
            PraiseChartsThumbnailUrl = req.PraiseChartsThumbnailUrl,
            Notes = req.Notes,
        };
        db.WorshipSongs.Add(song);
        await db.SaveChangesAsync();
        return Ok(MapSong(song));
    }

    [HttpPut("songs/{id}")]
    public async Task<IActionResult> UpdateSong(int id, [FromBody] SongPayload req)
    {
        if (!CanWorship()) return Forbid();
        var song = await db.WorshipSongs.FindAsync(id);
        if (song == null) return NotFound();
        song.Title = req.Title.Trim();
        song.Artist = req.Artist?.Trim() ?? "";
        song.DurationSeconds = req.DurationSeconds;
        song.Notes = req.Notes;
        await db.SaveChangesAsync();
        return Ok(MapSong(song));
    }

    [HttpDelete("songs/{id}")]
    public async Task<IActionResult> DeleteSong(int id)
    {
        if (!CanWorship()) return Forbid();
        var song = await db.WorshipSongs.FindAsync(id);
        if (song == null) return NotFound();
        // Remove uploaded files
        var files = JsonSerializer.Deserialize<List<WorshipFileEntry>>(song.FilesJson) ?? [];
        foreach (var f in files)
        {
            var fp = Path.Combine(UploadPath, f.Path);
            if (System.IO.File.Exists(fp)) System.IO.File.Delete(fp);
        }
        db.WorshipSongs.Remove(song);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/worship/songs/{id}/files — upload a file for a song
    [HttpPost("songs/{id}/files")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadFile(int id, IFormFile file)
    {
        if (!CanWorship()) return Forbid();
        var song = await db.WorshipSongs.FindAsync(id);
        if (song == null) return NotFound();
        if (file == null || file.Length == 0) return BadRequest(new { message = "No file." });

        Directory.CreateDirectory(UploadPath);
        var ext = Path.GetExtension(file.FileName);
        var safeName = $"{id}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(UploadPath, safeName);
        await using (var fs = System.IO.File.Create(fullPath))
            await file.CopyToAsync(fs);

        var files = JsonSerializer.Deserialize<List<WorshipFileEntry>>(song.FilesJson) ?? [];
        files.Add(new WorshipFileEntry(file.FileName, safeName, file.ContentType, file.Length));
        song.FilesJson = JsonSerializer.Serialize(files);
        await db.SaveChangesAsync();
        return Ok(MapSong(song));
    }

    // DELETE /api/worship/songs/{id}/files/{fileName}
    [HttpDelete("songs/{id}/files/{fileName}")]
    public async Task<IActionResult> DeleteFile(int id, string fileName)
    {
        if (!CanWorship()) return Forbid();
        var song = await db.WorshipSongs.FindAsync(id);
        if (song == null) return NotFound();
        var files = JsonSerializer.Deserialize<List<WorshipFileEntry>>(song.FilesJson) ?? [];
        var entry = files.FirstOrDefault(f => f.Path == fileName);
        if (entry == null) return NotFound();
        var fp = Path.Combine(UploadPath, fileName);
        if (System.IO.File.Exists(fp)) System.IO.File.Delete(fp);
        files.Remove(entry);
        song.FilesJson = JsonSerializer.Serialize(files);
        await db.SaveChangesAsync();
        return Ok(MapSong(song));
    }

    // GET /api/worship/songs/{id}/files/{fileName} — download
    [HttpGet("songs/{id}/files/{fileName}")]
    public IActionResult DownloadFile(int id, string fileName)
    {
        if (!CanWorship()) return Forbid();
        var safeName = Path.GetFileName(fileName);
        var fp = Path.Combine(UploadPath, safeName);
        if (!System.IO.File.Exists(fp)) return NotFound();
        var mime = MimeType(Path.GetExtension(safeName));
        return PhysicalFile(fp, mime, safeName);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SERVICE TYPES
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("service-types")]
    public async Task<IActionResult> GetServiceTypes()
    {
        if (!CanWorship()) return Forbid();
        var types = await db.WorshipServiceTypes.OrderBy(t => t.CreatedAt).ToListAsync();
        if (types.Count == 0)
        {
            var defaultSections = new List<ServiceTypeSectionDef> { new("Worship", 0) };
            var mainService = new WorshipServiceType
            {
                Name = "Main Service",
                SectionsJson = JsonSerializer.Serialize(defaultSections)
            };
            db.WorshipServiceTypes.Add(mainService);
            await db.SaveChangesAsync();
            types = [mainService];
        }
        return Ok(types.Select(t => new { t.Id, t.Name, sections = JsonSerializer.Deserialize<object>(t.SectionsJson) }));
    }

    [HttpPost("service-types")]
    public async Task<IActionResult> CreateServiceType([FromBody] ServiceTypePayload req)
    {
        if (!CanWorship()) return Forbid();
        var st = new WorshipServiceType { Name = req.Name.Trim(), SectionsJson = JsonSerializer.Serialize(req.Sections) };
        db.WorshipServiceTypes.Add(st);
        await db.SaveChangesAsync();
        return Ok(new { st.Id, st.Name, sections = req.Sections });
    }

    [HttpPut("service-types/{id}")]
    public async Task<IActionResult> UpdateServiceType(int id, [FromBody] ServiceTypePayload req)
    {
        if (!CanWorship()) return Forbid();
        var st = await db.WorshipServiceTypes.FindAsync(id);
        if (st == null) return NotFound();
        st.Name = req.Name.Trim();
        st.SectionsJson = JsonSerializer.Serialize(req.Sections);
        await db.SaveChangesAsync();
        return Ok(new { st.Id, st.Name, sections = req.Sections });
    }

    [HttpDelete("service-types/{id}")]
    public async Task<IActionResult> DeleteServiceType(int id)
    {
        if (!CanWorship()) return Forbid();
        var st = await db.WorshipServiceTypes.FindAsync(id);
        if (st == null) return NotFound();
        db.WorshipServiceTypes.Remove(st);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ════════════════════════════════════════════════════════════════════════
    // PLANS
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlan([FromQuery] int serviceTypeId, [FromQuery] string date)
    {
        if (!CanWorship()) return Forbid();
        if (!DateOnly.TryParse(date, out var d)) return BadRequest("Invalid date.");
        var plan = await db.WorshipPlans
            .Include(p => p.Sections).ThenInclude(s => s.Items).ThenInclude(i => i.Song)
            .FirstOrDefaultAsync(p => p.ServiceTypeId == serviceTypeId && p.PlanDate == d);
        if (plan == null) return Ok(null);
        return Ok(MapPlan(plan));
    }

    [HttpPost("plans")]
    public async Task<IActionResult> UpsertPlan([FromBody] PlanPayload req)
    {
        if (!CanWorship()) return Forbid();
        if (!DateOnly.TryParse(req.Date, out var d)) return BadRequest("Invalid date.");

        var plan = await db.WorshipPlans
            .Include(p => p.Sections).ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(p => p.ServiceTypeId == req.ServiceTypeId && p.PlanDate == d);

        if (plan == null)
        {
            plan = new WorshipPlan { ServiceTypeId = req.ServiceTypeId, PlanDate = d };
            db.WorshipPlans.Add(plan);
        }
        plan.UpdatedAt = DateTime.UtcNow;
        plan.StartTime = req.StartTime != null && TimeOnly.TryParse(req.StartTime, out var st) ? st : (TimeOnly?)null;

        // Sync sections
        var existingSecIds = plan.Sections.Select(s => s.Id).ToHashSet();
        var incomingSecIds = req.Sections.Where(s => s.Id > 0).Select(s => s.Id).ToHashSet();
        foreach (var toRemove in plan.Sections.Where(s => !incomingSecIds.Contains(s.Id)).ToList())
        {
            db.WorshipPlanItems.RemoveRange(toRemove.Items);
            db.WorshipPlanSections.Remove(toRemove);
        }

        for (var si = 0; si < req.Sections.Count; si++)
        {
            var sec = req.Sections[si];
            WorshipPlanSection section;
            if (sec.Id > 0 && plan.Sections.FirstOrDefault(s => s.Id == sec.Id) is { } existing)
            {
                section = existing;
                section.Title = sec.Title;
                section.Order = si;
            }
            else
            {
                section = new WorshipPlanSection { Plan = plan, Title = sec.Title, Order = si };
                db.WorshipPlanSections.Add(section);
                plan.Sections.Add(section);
            }

            // Sync items
            var existingItemIds = section.Items.Select(i => i.Id).ToHashSet();
            var incomingItemIds = sec.Items.Where(i => i.Id > 0).Select(i => i.Id).ToHashSet();
            foreach (var toRemove in section.Items.Where(i => !incomingItemIds.Contains(i.Id)).ToList())
                db.WorshipPlanItems.Remove(toRemove);

            for (var ii = 0; ii < sec.Items.Count; ii++)
            {
                var item = sec.Items[ii];
                if (item.Id > 0 && section.Items.FirstOrDefault(i => i.Id == item.Id) is { } existingItem)
                {
                    existingItem.SongId = item.SongId;
                    existingItem.EventTitle = item.EventTitle;
                    existingItem.LeaderName = item.LeaderName;
                    existingItem.DurationSeconds = item.DurationSeconds;
                    existingItem.Order = ii;
                }
                else
                {
                    db.WorshipPlanItems.Add(new WorshipPlanItem
                    {
                        Section = section,
                        SongId = item.SongId,
                        EventTitle = item.EventTitle,
                        LeaderName = item.LeaderName,
                        DurationSeconds = item.DurationSeconds,
                        Order = ii,
                    });
                }
            }
        }

        await db.SaveChangesAsync();

        // Reload with navigation properties
        var saved = await db.WorshipPlans
            .Include(p => p.Sections).ThenInclude(s => s.Items).ThenInclude(i => i.Song)
            .FirstAsync(p => p.Id == plan.Id);
        return Ok(MapPlan(saved));
    }

    [HttpDelete("plans/{id}")]
    public async Task<IActionResult> DeletePlan(int id)
    {
        if (!CanWorship()) return Forbid();
        var plan = await db.WorshipPlans.Include(p => p.Sections).ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (plan == null) return NotFound();
        foreach (var sec in plan.Sections) db.WorshipPlanItems.RemoveRange(sec.Items);
        db.WorshipPlanSections.RemoveRange(plan.Sections);
        db.WorshipPlans.Remove(plan);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════

    private static object MapSong(WorshipSong s) => new
    {
        s.Id, s.Title, s.Artist, s.DurationSeconds,
        s.PraiseChartsId, s.PraiseChartsSlug, s.PraiseChartsThumbnailUrl,
        s.Notes, s.CreatedAt,
        files = JsonSerializer.Deserialize<object>(s.FilesJson),
    };

    private static object MapPlan(WorshipPlan p) => new
    {
        p.Id, p.ServiceTypeId,
        date = p.PlanDate.ToString("yyyy-MM-dd"),
        startTime = p.StartTime.HasValue ? p.StartTime.Value.ToString("HH:mm") : null,
        p.UpdatedAt,
        sections = p.Sections.OrderBy(s => s.Order).Select(s => new
        {
            s.Id, s.Title, s.Order,
            items = s.Items.OrderBy(i => i.Order).Select(i => new
            {
                i.Id, i.SongId, i.EventTitle, i.LeaderName, i.DurationSeconds, i.Order,
                song = i.Song == null ? null : (object)new { i.Song.Id, i.Song.Title, i.Song.Artist, i.Song.DurationSeconds, i.Song.PraiseChartsThumbnailUrl },
            }),
        }),
    };

    private static string MimeType(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".m4a" => "audio/mp4",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream",
    };
}

public record SongPayload(string Title, string? Artist, int? DurationSeconds, string? PraiseChartsId,
    string? PraiseChartsSlug, string? PraiseChartsThumbnailUrl, string? Notes);
public record ServiceTypePayload(string Name, List<ServiceTypeSectionDef> Sections);
public record ServiceTypeSectionDef(string Title, int Order);
public record PlanPayload(int ServiceTypeId, string Date, List<PlanSectionPayload> Sections, string? StartTime = null);
public record PlanSectionPayload(int Id, string Title, List<PlanItemPayload> Items);
public record PlanItemPayload(int Id, int? SongId, string? EventTitle, string? LeaderName, int? DurationSeconds);

internal record WorshipFileEntry(string Name, string Path, string ContentType, long Size);

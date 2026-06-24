using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController(AppDbContext db) : ControllerBase
{
    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");
    private bool CanAttendance() =>
        IsSuperAdmin() ||
        (User.FindFirstValue("AllowedItemTypes") ?? "").Split(',').Contains("Attendance", StringComparer.OrdinalIgnoreCase);

    // ── Get attendance for a class on a given session date ───────────────────

    [HttpGet("{classId}/sessions/{date}")]
    public async Task<IActionResult> GetSession(int classId, string date)
    {
        if (!CanAttendance()) return Forbid();

        if (!DateOnly.TryParse(date, out var sessionDate))
            return BadRequest("Invalid date format (use YYYY-MM-DD)");

        var cls = await db.Classes
            .Include(c => c.Members).ThenInclude(m => m.User)
            .Include(c => c.ClassChildren).ThenInclude(cc => cc.Child)
            .FirstOrDefaultAsync(c => c.Id == classId);
        if (cls == null) return NotFound();

        var userAttendance = await db.ClassAttendances
            .Where(a => a.ClassId == classId && a.SessionDate == sessionDate)
            .Select(a => a.UserId)
            .ToListAsync();

        var childAttendance = await db.ChildAttendances
            .Where(a => a.ClassId == classId && a.SessionDate == sessionDate)
            .Select(a => a.ChildId)
            .ToListAsync();

        var members = cls.Members.Select(m => new
        {
            m.Id,
            m.UserId,
            m.Status,
            m.InviteEmail,
            m.InviteFirstName,
            m.InviteLastName,
            Name = m.User == null
                ? $"{m.InviteFirstName} {m.InviteLastName}".Trim()
                : $"{m.User.FirstName} {m.User.LastName}".Trim().NullIfEmpty() ?? m.User.Username,
            UserEmail = m.User?.Email,
            IsActive = m.User?.IsActive,
            Attended = m.UserId.HasValue && userAttendance.Contains(m.UserId.Value),
        });

        var children = cls.ClassChildren.Select(cc => new
        {
            cc.ChildId,
            cc.Child.FirstName,
            cc.Child.LastName,
            Attended = childAttendance.Contains(cc.ChildId),
        });

        return Ok(new { Members = members, Children = children });
    }

    // ── Mark / unmark user attendance ────────────────────────────────────────

    [HttpPost("{classId}/sessions/{date}/users/{userId}")]
    public async Task<IActionResult> MarkUser(int classId, string date, int userId)
    {
        if (!CanAttendance()) return Forbid();
        if (!DateOnly.TryParse(date, out var sessionDate)) return BadRequest("Invalid date");

        var exists = await db.ClassAttendances.AnyAsync(a =>
            a.ClassId == classId && a.UserId == userId && a.SessionDate == sessionDate);
        if (!exists)
        {
            db.ClassAttendances.Add(new ClassAttendance
            {
                ClassId = classId,
                UserId = userId,
                SessionDate = sessionDate,
            });
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    [HttpDelete("{classId}/sessions/{date}/users/{userId}")]
    public async Task<IActionResult> UnmarkUser(int classId, string date, int userId)
    {
        if (!CanAttendance()) return Forbid();
        if (!DateOnly.TryParse(date, out var sessionDate)) return BadRequest("Invalid date");

        var record = await db.ClassAttendances.FirstOrDefaultAsync(a =>
            a.ClassId == classId && a.UserId == userId && a.SessionDate == sessionDate);
        if (record != null)
        {
            db.ClassAttendances.Remove(record);
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    // ── Mark / unmark child attendance ───────────────────────────────────────

    [HttpPost("{classId}/sessions/{date}/children/{childId}")]
    public async Task<IActionResult> MarkChild(int classId, string date, int childId)
    {
        if (!CanAttendance()) return Forbid();
        if (!DateOnly.TryParse(date, out var sessionDate)) return BadRequest("Invalid date");

        var exists = await db.ChildAttendances.AnyAsync(a =>
            a.ClassId == classId && a.ChildId == childId && a.SessionDate == sessionDate);
        if (!exists)
        {
            db.ChildAttendances.Add(new ChildAttendance
            {
                ClassId = classId,
                ChildId = childId,
                SessionDate = sessionDate,
            });
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    [HttpDelete("{classId}/sessions/{date}/children/{childId}")]
    public async Task<IActionResult> UnmarkChild(int classId, string date, int childId)
    {
        if (!CanAttendance()) return Forbid();
        if (!DateOnly.TryParse(date, out var sessionDate)) return BadRequest("Invalid date");

        var record = await db.ChildAttendances.FirstOrDefaultAsync(a =>
            a.ClassId == classId && a.ChildId == childId && a.SessionDate == sessionDate);
        if (record != null)
        {
            db.ChildAttendances.Remove(record);
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    // ── Self-mark attendance (home page button, member marking themselves) ────

    [HttpPost("{classId}/self")]
    public async Task<IActionResult> MarkSelf(int classId)
    {
        var uid = CurrentUserId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Verify user is in the class
        var member = await db.ClassMembers.FirstOrDefaultAsync(m =>
            m.ClassId == classId && m.UserId == uid && m.Status == "Active");
        if (member == null) return Forbid();

        var exists = await db.ClassAttendances.AnyAsync(a =>
            a.ClassId == classId && a.UserId == uid && a.SessionDate == today);
        if (!exists)
        {
            db.ClassAttendances.Add(new ClassAttendance
            {
                ClassId = classId,
                UserId = uid,
                SessionDate = today,
            });
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    // ── Attendance report data (for print) ───────────────────────────────────

    [HttpGet("{classId}/report")]
    public async Task<IActionResult> GetReport(int classId, [FromQuery] string period, [FromQuery] int? year, [FromQuery] int? month)
    {
        if (!CanAttendance()) return Forbid();

        var cls = await db.Classes
            .Include(c => c.Members).ThenInclude(m => m.User)
            .Include(c => c.ClassChildren).ThenInclude(cc => cc.Child)
            .FirstOrDefaultAsync(c => c.Id == classId);
        if (cls == null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly from = period switch
        {
            "day" => today,
            "month" => new DateOnly(year ?? today.Year, month ?? today.Month, 1),
            "ytd" => new DateOnly(today.Year, 1, 1),
            _ => DateOnly.MinValue,  // all time
        };
        DateOnly to = period switch
        {
            "day" => today,
            "month" => new DateOnly(year ?? today.Year, month ?? today.Month, 1).AddMonths(1).AddDays(-1),
            _ => today,
        };

        var userRecords = await db.ClassAttendances
            .Where(a => a.ClassId == classId && a.SessionDate >= from && a.SessionDate <= to)
            .ToListAsync();

        var childRecords = await db.ChildAttendances
            .Where(a => a.ClassId == classId && a.SessionDate >= from && a.SessionDate <= to)
            .ToListAsync();

        var memberRows = cls.Members.Select(m => new
        {
            Name = m.User == null
                ? $"{m.InviteFirstName} {m.InviteLastName}".Trim()
                : $"{m.User.FirstName} {m.User.LastName}".Trim().NullIfEmpty() ?? m.User.Username,
            Sessions = userRecords.Where(r => r.UserId == m.UserId)
                .Select(r => r.SessionDate.ToString("yyyy-MM-dd")).OrderBy(d => d).ToList(),
        });

        var childRows = cls.ClassChildren.Select(cc => new
        {
            Name = $"{cc.Child.FirstName} {cc.Child.LastName}",
            Sessions = childRecords.Where(r => r.ChildId == cc.ChildId)
                .Select(r => r.SessionDate.ToString("yyyy-MM-dd")).OrderBy(d => d).ToList(),
        });

        return Ok(new
        {
            ClassName = cls.Title,
            Period = period,
            From = from.ToString("yyyy-MM-dd"),
            To = to.ToString("yyyy-MM-dd"),
            Members = memberRows,
            Children = childRows,
        });
    }
}

file static class AttendanceStringExt
{
    public static string? NullIfEmpty(this string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}

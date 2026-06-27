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

        var members = cls.Members.Where(m => !m.IsRemoved).Select(m => new
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

        var children = cls.ClassChildren.Where(cc => !cc.IsRemoved).Select(cc => new
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

    // ── Today's family attendance (home page load) ───────────────────────────

    [HttpGet("family/today")]
    public async Task<IActionResult> GetFamilyToday()
    {
        var uid = CurrentUserId();
        var me = await db.Users.FindAsync(uid);
        if (me == null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Collect family member IDs
        var userIds = new List<int> { uid };
        if (me.SpouseUserId.HasValue) userIds.Add(me.SpouseUserId.Value);

        var familyChildren = await db.Children
            .Where(c => c.ParentUserId.HasValue && userIds.Contains(c.ParentUserId.Value))
            .Select(c => c.Id)
            .ToListAsync();

        // Load today's user attendance
        var userAttendance = await db.ClassAttendances
            .Where(a => a.SessionDate == today && userIds.Contains(a.UserId))
            .Select(a => new { a.ClassId, a.UserId })
            .ToListAsync();

        // Load today's child attendance
        var childAttendance = await db.ChildAttendances
            .Where(a => a.SessionDate == today && familyChildren.Contains(a.ChildId))
            .Select(a => new { a.ClassId, a.ChildId })
            .ToListAsync();

        // Return as flat list of keys matching frontend format: "classId:self", "classId:spouse", "classId:child:childId"
        var spouseId = me.SpouseUserId;
        var keys = new List<string>();

        foreach (var a in userAttendance)
        {
            if (a.UserId == uid) keys.Add($"{a.ClassId}:self");
            else if (spouseId.HasValue && a.UserId == spouseId.Value) keys.Add($"{a.ClassId}:spouse");
        }
        foreach (var a in childAttendance)
            keys.Add($"{a.ClassId}:child:{a.ChildId}");

        return Ok(keys);
    }

    // ── Self-mark child attendance (home page) ───────────────────────────────

    [HttpPost("{classId}/self-child/{childId}")]
    public async Task<IActionResult> MarkSelfChild(int classId, int childId)
    {
        var uid = CurrentUserId();
        var me = await db.Users.FindAsync(uid);
        if (me == null) return NotFound();

        // Child must belong to user or their spouse
        var spouseId = me.SpouseUserId;
        var child = await db.Children.FirstOrDefaultAsync(c =>
            c.Id == childId && (c.ParentUserId == uid || (spouseId.HasValue && c.ParentUserId == spouseId.Value)));
        if (child == null) return Forbid();

        // Child must be in the class
        var inClass = await db.ClassChildren.AnyAsync(cc => cc.ClassId == classId && cc.ChildId == childId && !cc.IsRemoved);
        if (!inClass) return Forbid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var exists = await db.ChildAttendances.AnyAsync(a =>
            a.ClassId == classId && a.ChildId == childId && a.SessionDate == today);
        if (!exists)
        {
            db.ChildAttendances.Add(new ChildAttendance { ClassId = classId, ChildId = childId, SessionDate = today });
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    // ── Self-mark spouse attendance (home page) ───────────────────────────────

    [HttpPost("{classId}/self-spouse")]
    public async Task<IActionResult> MarkSelfSpouse(int classId)
    {
        var uid = CurrentUserId();
        var me = await db.Users.FindAsync(uid);
        if (me == null || !me.SpouseUserId.HasValue) return Forbid();

        var spouseId = me.SpouseUserId.Value;
        var member = await db.ClassMembers.FirstOrDefaultAsync(m =>
            m.ClassId == classId && m.UserId == spouseId && m.Status == "Active" && !m.IsRemoved);
        if (member == null) return Forbid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var exists = await db.ClassAttendances.AnyAsync(a =>
            a.ClassId == classId && a.UserId == spouseId && a.SessionDate == today);
        if (!exists)
        {
            db.ClassAttendances.Add(new ClassAttendance { ClassId = classId, UserId = spouseId, SessionDate = today });
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    // ── Self-mark everyone (user + spouse + children) ─────────────────────────

    [HttpPost("{classId}/self-family")]
    public async Task<IActionResult> MarkSelfFamily(int classId)
    {
        var uid = CurrentUserId();
        var me = await db.Users.FindAsync(uid);
        if (me == null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Mark self
        var selfMember = await db.ClassMembers.FirstOrDefaultAsync(m =>
            m.ClassId == classId && m.UserId == uid && m.Status == "Active" && !m.IsRemoved);
        if (selfMember != null && !await db.ClassAttendances.AnyAsync(a => a.ClassId == classId && a.UserId == uid && a.SessionDate == today))
            db.ClassAttendances.Add(new ClassAttendance { ClassId = classId, UserId = uid, SessionDate = today });

        // Mark spouse
        if (me.SpouseUserId.HasValue)
        {
            var spId = me.SpouseUserId.Value;
            var spMember = await db.ClassMembers.FirstOrDefaultAsync(m =>
                m.ClassId == classId && m.UserId == spId && m.Status == "Active" && !m.IsRemoved);
            if (spMember != null && !await db.ClassAttendances.AnyAsync(a => a.ClassId == classId && a.UserId == spId && a.SessionDate == today))
                db.ClassAttendances.Add(new ClassAttendance { ClassId = classId, UserId = spId, SessionDate = today });
        }

        // Mark children (own + spouse's)
        var parentIds = new List<int> { uid };
        if (me.SpouseUserId.HasValue) parentIds.Add(me.SpouseUserId.Value);

        var classChildren = await db.ClassChildren
            .Where(cc => cc.ClassId == classId && !cc.IsRemoved)
            .Select(cc => cc.ChildId)
            .ToListAsync();

        var familyChildren = await db.Children
            .Where(c => c.ParentUserId.HasValue && parentIds.Contains(c.ParentUserId.Value) && classChildren.Contains(c.Id))
            .ToListAsync();

        foreach (var child in familyChildren)
        {
            if (!await db.ChildAttendances.AnyAsync(a => a.ClassId == classId && a.ChildId == child.Id && a.SessionDate == today))
                db.ChildAttendances.Add(new ChildAttendance { ClassId = classId, ChildId = child.Id, SessionDate = today });
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Attendance report data (for print) ───────────────────────────────────

    [HttpGet("{classId}/report")]
    public async Task<IActionResult> GetReport(int classId, [FromQuery] string period, [FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? date)
    {
        if (!CanAttendance()) return Forbid();

        var cls = await db.Classes
            .Include(c => c.Members).ThenInclude(m => m.User)
            .Include(c => c.ClassChildren).ThenInclude(cc => cc.Child)
            .FirstOrDefaultAsync(c => c.Id == classId);
        if (cls == null) return NotFound();

        // Use client-supplied date for "day" to avoid UTC vs local-time mismatch
        var today = (date != null && DateOnly.TryParse(date, out var parsedDate))
            ? parsedDate
            : DateOnly.FromDateTime(DateTime.UtcNow);
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

        // Get check-in/out log for children in this class within the period
        var childIdsInClass = cls.ClassChildren.Where(cc => !cc.IsRemoved).Select(cc => cc.ChildId).ToList();
        var fromUtc = from == DateOnly.MinValue ? DateTime.MinValue : from.ToDateTime(TimeOnly.MinValue);
        var toUtc = to.ToDateTime(TimeOnly.MaxValue);
        var checkInLogs = childIdsInClass.Count > 0
            ? await db.ChildCheckIns
                .Where(ci => childIdsInClass.Contains(ci.ChildId) && ci.CheckedInAt >= fromUtc && ci.CheckedInAt <= toUtc)
                .OrderBy(ci => ci.CheckedInAt)
                .ToListAsync()
            : [];

        var memberRows = cls.Members.Where(m => !m.IsRemoved).Select(m => new
        {
            Name = m.User == null
                ? $"{m.InviteFirstName} {m.InviteLastName}".Trim()
                : $"{m.User.FirstName} {m.User.LastName}".Trim().NullIfEmpty() ?? m.User.Username,
            Sessions = userRecords.Where(r => r.UserId == m.UserId)
                .Select(r => r.SessionDate.ToString("yyyy-MM-dd")).OrderBy(d => d).ToList(),
        });

        var childRows = cls.ClassChildren.Where(cc => !cc.IsRemoved).Select(cc => new
        {
            Name = $"{cc.Child.FirstName} {cc.Child.LastName}",
            Sessions = childRecords.Where(r => r.ChildId == cc.ChildId)
                .Select(r => r.SessionDate.ToString("yyyy-MM-dd")).OrderBy(d => d).ToList(),
            CheckIns = checkInLogs.Where(ci => ci.ChildId == cc.ChildId).Select(ci => new
            {
                CheckedInAt = DateTime.SpecifyKind(ci.CheckedInAt, DateTimeKind.Utc),
                CheckedOutAt = ci.CheckedOutAt.HasValue ? DateTime.SpecifyKind(ci.CheckedOutAt.Value, DateTimeKind.Utc) : (DateTime?)null,
                ci.IsManual,
            }).ToList(),
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

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/checkins")]
[Authorize]
public class CheckInsController(AppDbContext db) : ControllerBase
{
    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");
    private bool CanManageAttendance() =>
        IsSuperAdmin() ||
        (User.FindFirstValue("AllowedItemTypes") ?? "").Split(',').Contains("Attendance", StringComparer.OrdinalIgnoreCase);

    private static object ChildDto(Child c) => new
    {
        c.Id, c.FirstName, c.LastName,
        BirthDate = c.BirthDate?.ToString("yyyy-MM-dd"),
        Gender = c.Gender?.ToString(),
        c.ParentUserId,
        ParentName = c.ParentUser != null ? $"{c.ParentUser.FirstName} {c.ParentUser.LastName}" : (string?)null
    };

    private static object CheckInDto(ChildCheckIn ci) => new
    {
        ci.Id, ci.ChildId,
        CheckedInAt = DateTime.SpecifyKind(ci.CheckedInAt, DateTimeKind.Utc),
        CheckedOutAt = ci.CheckedOutAt.HasValue ? DateTime.SpecifyKind(ci.CheckedOutAt.Value, DateTimeKind.Utc) : (DateTime?)null,
        ci.IsManual
    };

    // GET /api/checkins/today — list all check-ins for today
    [HttpGet("today")]
    public async Task<IActionResult> Today()
    {
        if (!CanManageAttendance()) return Forbid();
        var today = DateTime.UtcNow.Date;
        var checkIns = await db.ChildCheckIns
            .Include(ci => ci.Child)
            .Where(ci => ci.CheckedInAt.Date == today)
            .OrderByDescending(ci => ci.CheckedInAt)
            .ToListAsync();
        return Ok(checkIns.Select(ci => new
        {
            ci.Id,
            ci.ChildId,
            ChildName = $"{ci.Child.FirstName} {ci.Child.LastName}",
            CheckedInAt = DateTime.SpecifyKind(ci.CheckedInAt, DateTimeKind.Utc),
            CheckedOutAt = ci.CheckedOutAt.HasValue ? DateTime.SpecifyKind(ci.CheckedOutAt.Value, DateTimeKind.Utc) : (DateTime?)null,
            ci.IsManual
        }));
    }

    // POST /api/checkins/scan?token=xxx — QR scan: check in or out
    [HttpPost("scan")]
    public async Task<IActionResult> Scan([FromQuery] string token)
    {
        if (!CanManageAttendance()) return Forbid();
        var child = await db.Children
            .Include(c => c.ParentUser)
            .Include(c => c.ClassChildren.Where(cc => !cc.IsRemoved))
            .FirstOrDefaultAsync(c => c.CheckInToken == token);
        if (child == null) return NotFound(new { message = "Invalid QR code." });

        // Only block unverified children if ALL their active classes require a pass
        var anyClassRequiresPass = child.ClassChildren.Count == 0 ||
            await db.Classes.AnyAsync(c => child.ClassChildren.Select(cc => cc.ClassId).Contains(c.Id) && c.RequiresChildPass);
        if (!child.IsVerified && anyClassRequiresPass)
            return BadRequest(new { message = "This child is not verified for check-in." });

        var adminId = CurrentUserId();
        var today = DateTime.UtcNow.Date;
        var todayDate = DateOnly.FromDateTime(today);
        var existing = await db.ChildCheckIns
            .FirstOrDefaultAsync(ci => ci.ChildId == child.Id && ci.CheckedInAt.Date == today && ci.CheckedOutAt == null);

        if (existing != null)
        {
            existing.CheckedOutAt = DateTime.UtcNow;
            existing.CheckedOutByUserId = adminId;
            await db.SaveChangesAsync();
            return Ok(new { action = "checkout", child = ChildDto(child), checkIn = CheckInDto(existing) });
        }
        else
        {
            var checkIn = new ChildCheckIn { ChildId = child.Id, CheckedInByUserId = adminId };
            db.ChildCheckIns.Add(checkIn);

            // Mark attended only in classes that meet today (matching day of week)
            var todayDow = (int)today.DayOfWeek;
            var classIds = child.ClassChildren.Select(cc => cc.ClassId).ToList();
            if (classIds.Count > 0)
            {
                var classesForToday = await db.Classes
                    .Where(c => classIds.Contains(c.Id) && c.DayOfWeek == todayDow)
                    .Select(c => c.Id)
                    .ToListAsync();
                foreach (var classId in classesForToday)
                {
                    var alreadyMarked = await db.ChildAttendances.AnyAsync(a =>
                        a.ClassId == classId && a.ChildId == child.Id && a.SessionDate == todayDate);
                    if (!alreadyMarked)
                        db.ChildAttendances.Add(new ChildAttendance { ClassId = classId, ChildId = child.Id, SessionDate = todayDate });
                }
            }

            await db.SaveChangesAsync();
            return Ok(new { action = "checkin", child = ChildDto(child), checkIn = CheckInDto(checkIn) });
        }
    }

    // POST /api/checkins/{childId}/manual — manual check-in (no parent QR), prints 2 stickers
    [HttpPost("{childId}/manual")]
    public async Task<IActionResult> ManualCheckIn(int childId)
    {
        if (!CanManageAttendance()) return Forbid();
        var child = await db.Children.Include(c => c.ParentUser).FirstOrDefaultAsync(c => c.Id == childId);
        if (child == null) return NotFound();
        var adminId = CurrentUserId();
        var today = DateTime.UtcNow.Date;
        var existing = await db.ChildCheckIns
            .FirstOrDefaultAsync(ci => ci.ChildId == childId && ci.CheckedInAt.Date == today && ci.CheckedOutAt == null);
        if (existing != null) return Conflict(new { message = "Child is already checked in." });
        var checkIn = new ChildCheckIn { ChildId = childId, CheckedInByUserId = adminId, IsManual = true };
        db.ChildCheckIns.Add(checkIn);
        await db.SaveChangesAsync();
        return Ok(new { action = "checkin", child = ChildDto(child), checkIn = CheckInDto(checkIn), twoStickers = true });
    }

    // POST /api/checkins/{id}/checkout — manual check-out by check-in record ID
    [HttpPost("{id}/checkout")]
    public async Task<IActionResult> Checkout(int id)
    {
        if (!CanManageAttendance()) return Forbid();
        var checkIn = await db.ChildCheckIns.Include(ci => ci.Child).ThenInclude(c => c.ParentUser)
            .FirstOrDefaultAsync(ci => ci.Id == id);
        if (checkIn == null) return NotFound();
        if (checkIn.CheckedOutAt != null) return BadRequest(new { message = "Already checked out." });
        checkIn.CheckedOutAt = DateTime.UtcNow;
        checkIn.CheckedOutByUserId = CurrentUserId();
        await db.SaveChangesAsync();
        return Ok(new { action = "checkout", child = ChildDto(checkIn.Child), checkIn = CheckInDto(checkIn) });
    }
}

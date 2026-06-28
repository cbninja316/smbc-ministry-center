using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;
using SmbcStatusBoard.Api.Services;
using System.Security.Claims;

namespace SmbcStatusBoard.Api.Controllers;

public class ServeAssignmentResponse
{
    public int Id { get; set; }
    public string Status { get; set; } = "Pending";
    public string Date { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string RoleLabel { get; set; } = string.Empty;
    public string RoleDescription { get; set; } = string.Empty;
    public List<ServeTimeSlotResponse> TimeSlots { get; set; } = new();
    public WorshipPlanSummary? WorshipPlan { get; set; }
}

public class WorshipPlanSummary
{
    public int ServiceTypeId { get; set; }
    public string ServiceTypeName { get; set; } = string.Empty;
    public string? StartTime { get; set; }
    public List<WorshipPlanSummarySection> Sections { get; set; } = new();
}

public class WorshipPlanSummarySection
{
    public string Title { get; set; } = string.Empty;
    public List<WorshipPlanSummaryItem> Items { get; set; } = new();
}

public class WorshipPlanSummaryItem
{
    public string Title { get; set; } = string.Empty;
    public string? Artist { get; set; }
    public string? LeaderName { get; set; }
    public int? DurationSeconds { get; set; }
}

public class ServeTimeSlotResponse
{
    public string Time { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

[ApiController]
[Route("api/serve")]
[Authorize]
public class ServeController(AppDbContext db, EmailService emailService, IConfiguration config) : ControllerBase
{
    private int? CurrentUserId()
    {
        var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(s, out var id) ? id : null;
    }

    // GET /api/serve/my-assignments
    [HttpGet("my-assignments")]
    public async Task<IActionResult> GetMyAssignments()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var assignments = await db.VolunteerAssignments
            .Include(a => a.Role).ThenInclude(r => r.TimeSlots)
            .Include(a => a.Role).ThenInclude(r => r.WorshipServiceType)
            .Where(a => a.UserId == userId && a.Status != AssignmentStatus.Rejected)
            .OrderBy(a => a.SundayDate)
            .ToListAsync();

        var results = new List<ServeAssignmentResponse>();
        foreach (var a in assignments)
        {
            WorshipPlanSummary? planSummary = null;
            if (a.Role.WorshipServiceTypeId.HasValue)
            {
                var planDate = DateOnly.FromDateTime(a.SundayDate);
                var plan = await db.WorshipPlans
                    .Include(p => p.Sections).ThenInclude(s => s.Items).ThenInclude(i => i.Song)
                    .FirstOrDefaultAsync(p => p.ServiceTypeId == a.Role.WorshipServiceTypeId.Value && p.PlanDate == planDate);

                planSummary = new WorshipPlanSummary
                {
                    ServiceTypeId = a.Role.WorshipServiceTypeId.Value,
                    ServiceTypeName = a.Role.WorshipServiceType?.Name ?? "",
                    StartTime = plan?.StartTime?.ToString("HH:mm"),
                    Sections = plan?.Sections.OrderBy(s => s.Order).Select(s => new WorshipPlanSummarySection
                    {
                        Title = s.Title,
                        Items = s.Items.OrderBy(i => i.Order).Select(i => new WorshipPlanSummaryItem
                        {
                            Title = i.Song != null ? i.Song.Title : (i.EventTitle ?? ""),
                            Artist = i.Song?.Artist,
                            LeaderName = i.LeaderName,
                            DurationSeconds = i.DurationSeconds,
                        }).ToList()
                    }).ToList() ?? new()
                };
            }

            results.Add(new ServeAssignmentResponse
            {
                Id = a.Id,
                Status = a.Status.ToString(),
                Date = a.SundayDate.ToString("MMMM d, yyyy"),
                CreatedAt = a.CreatedAt.ToString("MMMM d, yyyy"),
                RoleId = a.RoleId,
                RoleLabel = a.Role.Label,
                RoleDescription = a.Role.Description,
                TimeSlots = a.Role.TimeSlots
                    .OrderBy(t => t.SortOrder)
                    .Select(t => new ServeTimeSlotResponse { Time = t.Time, Label = t.Label })
                    .ToList(),
                WorshipPlan = planSummary,
            });
        }
        return Ok(results);
    }

    // POST /api/serve/assignments/{id}/accept
    [HttpPost("assignments/{id}/accept")]
    public async Task<IActionResult> Accept(int id)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var assignment = await db.VolunteerAssignments
            .Include(a => a.Role)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

        if (assignment is null) return NotFound();

        assignment.Status = AssignmentStatus.Accepted;
        await db.SaveChangesAsync();
        await NotifyCoordinators(assignment, accepted: true);
        return Ok(new { status = "Accepted" });
    }

    // POST /api/serve/assignments/{id}/reject
    [HttpPost("assignments/{id}/reject")]
    public async Task<IActionResult> Reject(int id)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var assignment = await db.VolunteerAssignments
            .Include(a => a.Role)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

        if (assignment is null) return NotFound();

        assignment.Status = AssignmentStatus.Rejected;
        await db.SaveChangesAsync();
        await NotifyCoordinators(assignment, accepted: false);
        return Ok(new { status = "Rejected" });
    }

    // POST /api/serve/assignments/{id}/resend — re-notify coordinators of acceptance
    [HttpPost("assignments/{id}/resend")]
    public async Task<IActionResult> Resend(int id)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var assignment = await db.VolunteerAssignments
            .Include(a => a.Role)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId && a.Status == AssignmentStatus.Accepted);

        if (assignment is null) return NotFound();

        await NotifyCoordinators(assignment, accepted: true);
        return Ok(new { message = "Acceptance resent." });
    }

    private async Task NotifyCoordinators(VolunteerAssignment assignment, bool accepted)
    {
        try
        {
            var recipients = await db.Users
                .Where(u => u.IsActive && (
                    u.Role == UserRole.SuperAdmin ||
                    u.AllowedItemTypes.Contains("AssignVolunteers")))
                .Select(u => new { u.Email, u.Username })
                .ToListAsync();

            await emailService.SendVolunteerResponseAsync(
                recipients.Select(r => (r.Email, r.Username)).ToList(),
                assignment.User.Username,
                assignment.Role.Label,
                assignment.SundayDate.ToString("MMMM d, yyyy"),
                accepted);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Email] Failed to notify coordinators: {ex.Message}");
        }
    }
}

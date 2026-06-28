using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.DTOs;
using SmbcStatusBoard.Api.Models;
using SmbcStatusBoard.Api.Services;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class VolunteerController(AppDbContext db, EmailService emailService, IConfiguration config) : ControllerBase
{
    private bool CanManageVolunteers()
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (role == "SuperAdmin") return true;
        var allowed = User.FindFirst("AllowedItemTypes")?.Value ?? "";
        return allowed.Split(',', StringSplitOptions.RemoveEmptyEntries).Contains("AssignVolunteers");
    }

    private static VolunteerRoleResponse ToRoleResponse(VolunteerRole r) => new()
    {
        Id = r.Id, Label = r.Label, Description = r.Description,
        SortOrder = r.SortOrder, SpecialEventId = r.SpecialEventId,
        WorshipServiceTypeId = r.WorshipServiceTypeId,
        WorshipServiceTypeName = r.WorshipServiceType?.Name,
        TimeSlots = r.TimeSlots.OrderBy(t => t.SortOrder).Select(t => new TimeSlotResponse
        {
            Id = t.Id, Time = t.Time, Label = t.Label, SortOrder = t.SortOrder
        }).ToList()
    };

    // GET /api/volunteer-roles?specialEventId=X  (omit param for Sunday roles)
    [HttpGet("volunteer-roles")]
    public async Task<IActionResult> GetRoles([FromQuery] int? specialEventId)
    {
        if (!CanManageVolunteers()) return Forbid();
        var roles = await db.VolunteerRoles
            .Include(r => r.TimeSlots)
            .Include(r => r.WorshipServiceType)
            .Where(r => r.SpecialEventId == specialEventId)
            .OrderBy(r => r.SortOrder)
            .ToListAsync();
        return Ok(roles.Select(ToRoleResponse));
    }

    // POST /api/volunteer-roles
    [HttpPost("volunteer-roles")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest req)
    {
        if (!CanManageVolunteers()) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Label)) return BadRequest(new { message = "Label is required." });
        var count = await db.VolunteerRoles.CountAsync(r => r.SpecialEventId == req.SpecialEventId);
        var role = new VolunteerRole { Label = req.Label, Description = req.Description ?? "", SortOrder = count, SpecialEventId = req.SpecialEventId, WorshipServiceTypeId = req.WorshipServiceTypeId };
        if (req.TimeSlots != null)
            for (int i = 0; i < req.TimeSlots.Count; i++)
                role.TimeSlots.Add(new RoleTimeSlot { Time = req.TimeSlots[i].Time, Label = req.TimeSlots[i].Label, SortOrder = i });
        db.VolunteerRoles.Add(role);
        await db.SaveChangesAsync();
        await db.Entry(role).Collection(r => r.TimeSlots).LoadAsync();
        await db.Entry(role).Reference(r => r.WorshipServiceType).LoadAsync();
        return CreatedAtAction(nameof(GetRoles), ToRoleResponse(role));
    }

    // PUT /api/volunteer-roles/{id}
    [HttpPut("volunteer-roles/{id}")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleRequest req)
    {
        if (!CanManageVolunteers()) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Label)) return BadRequest(new { message = "Label is required." });
        var role = await db.VolunteerRoles.Include(r => r.TimeSlots).Include(r => r.WorshipServiceType).FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return NotFound();
        role.Label = req.Label;
        role.Description = req.Description ?? "";
        role.WorshipServiceTypeId = req.WorshipServiceTypeId;
        // Replace time slots
        db.RoleTimeSlots.RemoveRange(role.TimeSlots);
        role.TimeSlots.Clear();
        if (req.TimeSlots != null)
            for (int i = 0; i < req.TimeSlots.Count; i++)
                role.TimeSlots.Add(new RoleTimeSlot { Time = req.TimeSlots[i].Time, Label = req.TimeSlots[i].Label, SortOrder = i });
        await db.SaveChangesAsync();
        return Ok(ToRoleResponse(role));
    }

    // DELETE /api/volunteer-roles/{id}
    [HttpDelete("volunteer-roles/{id}")]
    public async Task<IActionResult> DeleteRole(int id)
    {
        if (!CanManageVolunteers()) return Forbid();
        var role = await db.VolunteerRoles.FindAsync(id);
        if (role is null) return NotFound();
        var today = DateTime.UtcNow.Date;
        var futureAssignments = await db.VolunteerAssignments
            .Where(a => a.RoleId == id && a.SundayDate >= today)
            .ToListAsync();
        db.VolunteerAssignments.RemoveRange(futureAssignments);
        db.VolunteerRoles.Remove(role);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/volunteer-assignments?sunday=YYYY-MM-DD
    [HttpGet("volunteer-assignments")]
    public async Task<IActionResult> GetAssignments([FromQuery] string sunday)
    {
        if (!CanManageVolunteers()) return Forbid();
        if (!DateTime.TryParse(sunday, out var sundayDate)) return BadRequest(new { message = "Invalid sunday date." });
        var assignments = await db.VolunteerAssignments
            .Include(a => a.Role)
            .Include(a => a.User)
            .Where(a => a.SundayDate.Date == sundayDate.Date)
            .Select(a => new VolunteerAssignmentResponse
            {
                Id = a.Id,
                RoleId = a.RoleId,
                RoleLabel = a.Role.Label,
                UserId = a.UserId,
                Username = a.User.Username,
                UserEmail = a.User.Email,
                SundayDate = a.SundayDate,
                Status = a.Status.ToString()
            })
            .ToListAsync();
        return Ok(assignments);
    }

    // POST /api/volunteer-assignments
    [HttpPost("volunteer-assignments")]
    public async Task<IActionResult> CreateAssignment([FromBody] CreateAssignmentRequest req)
    {
        if (!CanManageVolunteers()) return Forbid();

        var role = await db.VolunteerRoles.Include(r => r.TimeSlots).FirstOrDefaultAsync(r => r.Id == req.RoleId);
        if (role is null) return NotFound(new { message = "Role not found." });

        var user = await db.Users.FindAsync(req.UserId);
        if (user is null) return NotFound(new { message = "User not found." });

        var assignment = new VolunteerAssignment
        {
            RoleId = req.RoleId,
            UserId = req.UserId,
            SundayDate = req.SundayDate.Date,
            Status = AssignmentStatus.Pending,
            ResponseToken = Guid.NewGuid().ToString("N")
        };
        db.VolunteerAssignments.Add(assignment);
        await db.SaveChangesAsync();

        // Send email to volunteer
        try
        {
            var frontendBase = config["App:NextFrontendUrl"] ?? "http://localhost:3000";
            var acceptUrl = $"{frontendBase}/api/public/volunteer-respond?token={assignment.ResponseToken}&response=accept";
            var rejectUrl = $"{frontendBase}/api/public/volunteer-respond?token={assignment.ResponseToken}&response=reject";
            var timeSlots = role.TimeSlots
                .OrderBy(t => t.SortOrder)
                .Select(t => (t.Time, t.Label))
                .ToList();
            await emailService.SendVolunteerRequestAsync(
                user.Email, user.Username, role.Label, role.Description,
                assignment.SundayDate.ToString("MMMM d, yyyy"),
                acceptUrl, rejectUrl, timeSlots);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Email] Failed to send volunteer request: {ex.Message}");
        }

        var response = new VolunteerAssignmentResponse
        {
            Id = assignment.Id,
            RoleId = assignment.RoleId,
            RoleLabel = role.Label,
            UserId = assignment.UserId,
            Username = user.Username,
            UserEmail = user.Email,
            SundayDate = assignment.SundayDate,
            Status = assignment.Status.ToString()
        };
        return Created("", response);
    }

    // DELETE /api/volunteer-assignments/{id}
    [HttpDelete("volunteer-assignments/{id}")]
    public async Task<IActionResult> DeleteAssignment(int id)
    {
        if (!CanManageVolunteers()) return Forbid();
        var assignment = await db.VolunteerAssignments
            .Include(a => a.Role)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (assignment is null) return NotFound();

        db.VolunteerAssignments.Remove(assignment);
        await db.SaveChangesAsync();

        try
        {
            await emailService.SendVolunteerCancellationAsync(
                assignment.User.Email,
                assignment.User.Username,
                assignment.Role.Label,
                assignment.SundayDate.ToString("MMMM d, yyyy"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Email] Failed to send cancellation: {ex.Message}");
        }

        return NoContent();
    }
}

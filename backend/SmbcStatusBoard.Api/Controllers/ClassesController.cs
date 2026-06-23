using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;
using SmbcStatusBoard.Api.Services;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/classes")]
[Authorize]
public class ClassesController(AppDbContext db, EmailService email, IConfiguration config) : ControllerBase
{
    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");
    private bool CanManageClasses() =>
        IsSuperAdmin() ||
        (User.FindFirstValue("AllowedItemTypes") ?? "").Split(',').Contains("Classes", StringComparer.OrdinalIgnoreCase);

    // ── List ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!CanManageClasses()) return Forbid();

        var classes = await db.Classes
            .Include(c => c.Members).ThenInclude(m => m.User)
            .Include(c => c.ClassChildren).ThenInclude(cc => cc.Child)
            .Include(c => c.PromotionClass)
            .OrderBy(c => c.Type).ThenBy(c => c.Title)
            .ToListAsync();

        return Ok(classes.Select(MapClass));
    }

    // GET my classes (for home page — any authenticated user)
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var uid = CurrentUserId();
        var memberships = await db.ClassMembers
            .Where(m => m.UserId == uid && m.Status == "Active")
            .Include(m => m.Class)
            .ToListAsync();
        return Ok(memberships.Select(m => MapClass(m.Class)));
    }

    // ── Create ───────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ClassPayload req)
    {
        if (!CanManageClasses()) return Forbid();

        var cls = new Class
        {
            Title = req.Title.Trim(),
            Description = req.Description.Trim(),
            DayOfWeek = req.DayOfWeek,
            ClassTime = req.ClassTime.Trim(),
            Type = req.Type,
            PromotionClassId = req.PromotionClassId,
        };
        db.Classes.Add(cls);
        await db.SaveChangesAsync();
        return Ok(MapClass(cls));
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ClassPayload req)
    {
        if (!CanManageClasses()) return Forbid();

        var cls = await db.Classes.FindAsync(id);
        if (cls == null) return NotFound();

        cls.Title = req.Title.Trim();
        cls.Description = req.Description.Trim();
        cls.DayOfWeek = req.DayOfWeek;
        cls.ClassTime = req.ClassTime.Trim();
        cls.Type = req.Type;
        cls.PromotionClassId = req.PromotionClassId;
        await db.SaveChangesAsync();
        return Ok(MapClass(cls));
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsSuperAdmin()) return Forbid();
        var cls = await db.Classes.FindAsync(id);
        if (cls == null) return NotFound();
        db.Classes.Remove(cls);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Members ──────────────────────────────────────────────────────────────

    // Add existing user
    [HttpPost("{id}/members/user")]
    public async Task<IActionResult> AddUser(int id, [FromBody] AddUserRequest req)
    {
        if (!CanManageClasses()) return Forbid();

        var cls = await db.Classes.FindAsync(id);
        if (cls == null) return NotFound("Class not found");

        var user = await db.Users.FindAsync(req.UserId);
        if (user == null) return NotFound("User not found");

        var exists = await db.ClassMembers.AnyAsync(m => m.ClassId == id && m.UserId == req.UserId);
        if (exists) return BadRequest("User is already in this class");

        var member = new ClassMember { ClassId = id, UserId = req.UserId, Status = "Active" };
        db.ClassMembers.Add(member);
        await db.SaveChangesAsync();

        // Notify existing user by email
        var name = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrEmpty(name)) name = user.Username;
        var loginUrl = config["App:FrontendUrl"]?.Split(',')[0].Trim() ?? "";
        try { await email.SendClassAddedNotificationAsync(user.Email, name, cls.Title, loginUrl); } catch { }

        return Ok(new { member.Id, member.UserId, member.Status });
    }

    // Invite new (non-user) adult by email
    [HttpPost("{id}/members/invite")]
    public async Task<IActionResult> InviteNew(int id, [FromBody] InviteNewMemberRequest req)
    {
        if (!CanManageClasses()) return Forbid();

        var cls = await db.Classes.FindAsync(id);
        if (cls == null) return NotFound("Class not found");
        if (cls.Type != ClassType.Adult) return BadRequest("External invites are only for adult classes");

        var normalEmail = req.Email.Trim().ToLower();

        // If user already exists, add them directly
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == normalEmail);
        if (existing != null)
        {
            var alreadyIn = await db.ClassMembers.AnyAsync(m => m.ClassId == id && m.UserId == existing.Id);
            if (alreadyIn) return BadRequest("User is already in this class");

            db.ClassMembers.Add(new ClassMember { ClassId = id, UserId = existing.Id, Status = "Active" });
            await db.SaveChangesAsync();
            var eName = $"{existing.FirstName} {existing.LastName}".Trim();
            if (string.IsNullOrEmpty(eName)) eName = existing.Username;
            var loginUrl2 = config["App:FrontendUrl"]?.Split(',')[0].Trim() ?? "";
            try { await email.SendClassAddedNotificationAsync(existing.Email, eName, cls.Title, loginUrl2); } catch { }
            return Ok(new { message = "Existing user added and notified" });
        }

        // Check for duplicate pending invite
        var dupPending = await db.ClassMembers.AnyAsync(m => m.ClassId == id && m.InviteEmail == normalEmail);
        if (dupPending) return BadRequest("An invitation has already been sent to that email");

        // Create inactive user
        var firstName = req.FirstName.Trim();
        var lastName = req.LastName.Trim();
        var baseUsername = (firstName + lastName).Replace(" ", "");
        var username = baseUsername;
        var suffix = 1;
        while (await db.Users.AnyAsync(u => u.Username == username))
            username = baseUsername + suffix++;

        var newUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = normalEmail,
            Username = username,
            Role = UserRole.Member,
            IsActive = false,
            EmailVerified = false,
            AllowedItemTypes = string.Empty,
        };
        db.Users.Add(newUser);
        await db.SaveChangesAsync();

        // Invite token (7 days)
        var token = Guid.NewGuid().ToString("N");
        db.InviteTokens.Add(new InviteToken
        {
            UserId = newUser.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        });

        // ClassMember (pending)
        var member = new ClassMember
        {
            ClassId = id,
            UserId = newUser.Id,
            Status = "Pending",
            InviteEmail = normalEmail,
            InviteFirstName = firstName,
            InviteLastName = lastName,
        };
        db.ClassMembers.Add(member);
        await db.SaveChangesAsync();

        var frontendUrl = config["App:FrontendUrl"]?.Split(',')[0].Trim() ?? "";
        var joinLink = $"{frontendUrl}/setup-password?token={token}";
        try { await email.SendClassInviteAsync(normalEmail, firstName, cls.Title, joinLink); } catch { }

        return Ok(new { message = "Invitation sent" });
    }

    // Remove a member
    [HttpDelete("{id}/members/{memberId}")]
    public async Task<IActionResult> RemoveMember(int id, int memberId)
    {
        if (!CanManageClasses()) return Forbid();
        var member = await db.ClassMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.ClassId == id);
        if (member == null) return NotFound();
        db.ClassMembers.Remove(member);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Children ─────────────────────────────────────────────────────────────

    [HttpPost("{id}/children")]
    public async Task<IActionResult> AddChild(int id, [FromBody] AddChildRequest req)
    {
        if (!CanManageClasses()) return Forbid();
        var cls = await db.Classes.FindAsync(id);
        if (cls == null) return NotFound("Class not found");
        if (cls.Type != ClassType.Children) return BadRequest("Children can only be added to children's classes");

        var child = await db.Children.FindAsync(req.ChildId);
        if (child == null) return NotFound("Child not found");

        var exists = await db.ClassChildren.AnyAsync(cc => cc.ClassId == id && cc.ChildId == req.ChildId);
        if (exists) return BadRequest("Child is already in this class");

        db.ClassChildren.Add(new ClassChild { ClassId = id, ChildId = req.ChildId });
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}/children/{childId}")]
    public async Task<IActionResult> RemoveChild(int id, int childId)
    {
        if (!CanManageClasses()) return Forbid();
        var cc = await db.ClassChildren.FirstOrDefaultAsync(cc => cc.ClassId == id && cc.ChildId == childId);
        if (cc == null) return NotFound();
        db.ClassChildren.Remove(cc);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Promotion Sunday ─────────────────────────────────────────────────────

    [HttpPost("{id}/promote")]
    public async Task<IActionResult> PromotionSunday(int id)
    {
        if (!CanManageClasses()) return Forbid();

        var cls = await db.Classes
            .Include(c => c.ClassChildren)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cls == null) return NotFound();
        if (cls.PromotionClassId == null) return BadRequest("No promotion class is assigned to this class");

        var targetId = cls.PromotionClassId.Value;
        var target = await db.Classes.FindAsync(targetId);
        if (target == null) return NotFound("Promotion class not found");

        var moved = 0;
        foreach (var cc in cls.ClassChildren.ToList())
        {
            var alreadyInTarget = await db.ClassChildren
                .AnyAsync(t => t.ClassId == targetId && t.ChildId == cc.ChildId);
            if (!alreadyInTarget)
            {
                db.ClassChildren.Add(new ClassChild { ClassId = targetId, ChildId = cc.ChildId });
                moved++;
            }
            db.ClassChildren.Remove(cc);
        }
        await db.SaveChangesAsync();

        return Ok(new { message = $"{moved} child(ren) promoted to {target.Title}" });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static object MapClass(Class c) => new
    {
        c.Id,
        c.Title,
        c.Description,
        c.DayOfWeek,
        c.ClassTime,
        Type = c.Type.ToString(),
        c.PromotionClassId,
        PromotionClassName = c.PromotionClass?.Title,
        c.CreatedAt,
        Members = c.Members?.Select(m => new
        {
            m.Id,
            m.UserId,
            m.Status,
            m.InviteEmail,
            m.InviteFirstName,
            m.InviteLastName,
            m.AddedAt,
            UserName = m.User == null ? null : $"{m.User.FirstName} {m.User.LastName}".Trim().NullIfEmpty() ?? m.User.Username,
            UserEmail = m.User?.Email,
            IsActive = m.User?.IsActive,
        }) ?? [],
        Children = c.ClassChildren?.Select(cc => new
        {
            cc.ChildId,
            cc.Child.FirstName,
            cc.Child.LastName,
            cc.AddedAt,
        }) ?? [],
    };
}

file static class StringExtensions
{
    public static string? NullIfEmpty(this string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}

public record ClassPayload(string Title, string Description, int DayOfWeek, string ClassTime, ClassType Type, int? PromotionClassId);
public record AddUserRequest(int UserId);
public record InviteNewMemberRequest(string FirstName, string LastName, string Email);
public record AddChildRequest(int ChildId);

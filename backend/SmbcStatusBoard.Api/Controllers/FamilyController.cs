using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;
using SmbcStatusBoard.Api.Services;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/family")]
[Authorize]
public class FamilyController(AppDbContext db, EmailService email, IConfiguration config) : ControllerBase
{
    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");

    // ── Get my family ─────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetMyFamily()
    {
        var uid = CurrentUserId();
        var user = await db.Users
            .Include(u => u.Spouse).ThenInclude(s => s!.Children).ThenInclude(c => c.ClassChildren.Where(cc => !cc.IsRemoved))
            .Include(u => u.Children).ThenInclude(c => c.ClassChildren.Where(cc => !cc.IsRemoved))
            .FirstOrDefaultAsync(u => u.Id == uid);
        if (user == null) return NotFound();

        // Combine own children + spouse's children, deduplicated by id
        var allChildren = user.Children
            .Concat(user.Spouse?.Children ?? [])
            .DistinctBy(c => c.Id);

        return Ok(new
        {
            spouse = user.Spouse == null ? null : new
            {
                user.Spouse.Id,
                FirstName = string.IsNullOrWhiteSpace(user.Spouse.FirstName)
                    ? System.Text.RegularExpressions.Regex.Replace(user.Spouse.Username, "([a-z])([A-Z])", "$1 $2").Split(' ')[0]
                    : user.Spouse.FirstName,
                LastName = string.IsNullOrWhiteSpace(user.Spouse.LastName)
                    ? string.Join(" ", System.Text.RegularExpressions.Regex.Replace(user.Spouse.Username, "([a-z])([A-Z])", "$1 $2").Split(' ').Skip(1))
                    : user.Spouse.LastName,
                user.Spouse.Email,
                BirthDate = user.Spouse.BirthDate?.ToString("yyyy-MM-dd"),
            },
            children = allChildren.Select(c => new
            {
                c.Id,
                c.FirstName,
                c.LastName,
                BirthDate = c.BirthDate?.ToString("yyyy-MM-dd"),
                inAnyClass = c.ClassChildren.Any(cc => !cc.IsRemoved),
            }),
        });
    }

    // ── Spouse ────────────────────────────────────────────────────────────────

    [HttpPost("spouse")]
    public async Task<IActionResult> SetSpouse([FromBody] SpouseRequest req)
    {
        var uid = CurrentUserId();
        var me = await db.Users.FindAsync(uid);
        if (me == null) return NotFound();

        var normalEmail = req.Email.Trim().ToLower();
        var firstName = req.FirstName.Trim();
        var lastName = req.LastName.Trim();

        // If a member with that email already exists...
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == normalEmail && u.Id != uid);
        if (existing != null)
        {
            me.SpouseUserId = existing.Id;
            await db.SaveChangesAsync();

            // Active user — link both directions, no email needed
            if (existing.IsActive)
            {
                existing.SpouseUserId = uid;
                await db.SaveChangesAsync();
                return Ok(new { existing.Id, existing.FirstName, existing.LastName, existing.Email, invited = false });
            }

            // Inactive (previously invited but never activated) — expire old tokens and resend
            var oldTokens = await db.InviteTokens.Where(t => t.UserId == existing.Id && !t.Used).ToListAsync();
            foreach (var t in oldTokens) t.Used = true;

            var newToken = Guid.NewGuid().ToString("N");
            db.InviteTokens.Add(new InviteToken { UserId = existing.Id, Token = newToken, ExpiresAt = DateTime.UtcNow.AddDays(7) });
            await db.SaveChangesAsync();

            var frontendUrlExisting = config["App:NextFrontendUrl"]?.Trim()
                ?? config["App:FrontendUrl"]?.Split(',')[0].Trim() ?? "";
            var joinLinkExisting = $"{frontendUrlExisting}/setup-password?token={newToken}";
            var myNameExisting = $"{me.FirstName} {me.LastName}".Trim();
            if (string.IsNullOrEmpty(myNameExisting)) myNameExisting = me.Username;
            try { await email.SendSpouseInviteAsync(existing.Email, existing.FirstName, myNameExisting, existing.Username, joinLinkExisting); } catch { }

            return Ok(new { existing.Id, existing.FirstName, existing.LastName, existing.Email, invited = true });
        }

        // No account found — create inactive user and send invite
        // Username rule: Capitalize(first) + Capitalize(last) — letters only, first char upper, rest lower
        static string CapWord(string s)
        {
            var letters = new string(s.Where(char.IsLetter).ToArray());
            return letters.Length == 0 ? "" : char.ToUpper(letters[0]) + letters[1..].ToLower();
        }
        var baseUsername = CapWord(firstName) + CapWord(lastName);
        if (string.IsNullOrEmpty(baseUsername)) baseUsername = "Member";
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

        var token = Guid.NewGuid().ToString("N");
        db.InviteTokens.Add(new InviteToken
        {
            UserId = newUser.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        });

        me.SpouseUserId = newUser.Id;
        await db.SaveChangesAsync();

        // Prefer NextFrontendUrl (single value) then first entry of FrontendUrl
        var frontendUrl = config["App:NextFrontendUrl"]?.Trim()
            ?? config["App:FrontendUrl"]?.Split(',')[0].Trim()
            ?? "";
        var joinLink = $"{frontendUrl}/setup-password?token={token}";
        var myName = $"{me.FirstName} {me.LastName}".Trim();
        if (string.IsNullOrEmpty(myName)) myName = me.Username;
        try { await email.SendSpouseInviteAsync(normalEmail, firstName, myName, username, joinLink); } catch { }

        return Ok(new { newUser.Id, newUser.FirstName, newUser.LastName, newUser.Email, invited = true });
    }

    [HttpDelete("spouse")]
    public async Task<IActionResult> RemoveSpouse()
    {
        var uid = CurrentUserId();
        var me = await db.Users.Include(u => u.Spouse).FirstOrDefaultAsync(u => u.Id == uid);
        if (me == null) return NotFound();
        // Clear both directions
        if (me.Spouse != null && me.Spouse.SpouseUserId == uid)
            me.Spouse.SpouseUserId = null;
        me.SpouseUserId = null;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Children ──────────────────────────────────────────────────────────────

    [HttpPost("children")]
    public async Task<IActionResult> AddChild([FromBody] FamilyChildRequest req)
    {
        var uid = CurrentUserId();

        var firstName = req.FirstName.Trim();
        var lastName = req.LastName.Trim();
        var birthDate = req.BirthDate is not null ? DateOnly.Parse(req.BirthDate) : (DateOnly?)null;

        // Check for an exact first + last + birthdate match among existing children
        var exactMatch = birthDate.HasValue
            ? await db.Children.FirstOrDefaultAsync(c =>
                c.FirstName.ToLower() == firstName.ToLower() &&
                c.LastName.ToLower() == lastName.ToLower() &&
                c.BirthDate == birthDate)
            : null;

        if (exactMatch != null)
        {
            // Link the existing child to this parent instead of creating a duplicate
            exactMatch.ParentUserId = uid;

            // Alert the admin dashboard via a suggestion so they are aware of the link
            var alreadySuggested = await db.ChildLinkSuggestions
                .AnyAsync(s => s.RequestingUserId == uid && s.SuggestedChildId == exactMatch.Id);
            if (!alreadySuggested)
            {
                // Use the existing child as both the "new" and "suggested" child so the admin
                // sees the link event. We create a minimal placeholder child to satisfy the FK.
                var placeholder = new Child { FirstName = firstName, LastName = lastName, BirthDate = birthDate };
                db.Children.Add(placeholder);
                await db.SaveChangesAsync();

                db.ChildLinkSuggestions.Add(new ChildLinkSuggestion
                {
                    RequestingUserId = uid,
                    NewChildId = placeholder.Id,
                    SuggestedChildId = exactMatch.Id,
                });
            }

            await db.SaveChangesAsync();
            return Ok(new { exactMatch.Id, exactMatch.FirstName, exactMatch.LastName, hasSuggestions = true });
        }

        // No exact match — create a new child record
        var child = new Child
        {
            FirstName = firstName,
            LastName = lastName,
            ParentUserId = uid,
            BirthDate = birthDate,
            Gender = req.Gender is not null && Enum.TryParse<Gender>(req.Gender, true, out var fcg) ? fcg : null,
        };
        db.Children.Add(child);
        await db.SaveChangesAsync();

        // Check for name-only collisions (existing records may lack birthdates) and suggest
        var nameMatches = await db.Children
            .Where(c => c.Id != child.Id &&
                c.FirstName.ToLower() == firstName.ToLower() &&
                c.LastName.ToLower() == lastName.ToLower())
            .ToListAsync();

        foreach (var match in nameMatches)
        {
            var alreadySuggested = await db.ChildLinkSuggestions
                .AnyAsync(s => s.RequestingUserId == uid && s.NewChildId == child.Id && s.SuggestedChildId == match.Id);
            if (!alreadySuggested)
            {
                db.ChildLinkSuggestions.Add(new ChildLinkSuggestion
                {
                    RequestingUserId = uid,
                    NewChildId = child.Id,
                    SuggestedChildId = match.Id,
                });
            }
        }
        if (nameMatches.Count > 0) await db.SaveChangesAsync();

        return Ok(new { child.Id, child.FirstName, child.LastName, hasSuggestions = nameMatches.Count > 0 });
    }

    [HttpDelete("children/{childId}")]
    public async Task<IActionResult> RemoveChild(int childId)
    {
        var uid = CurrentUserId();
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == childId && c.ParentUserId == uid);
        if (child == null) return NotFound();
        child.ParentUserId = null;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Admin: all family groups ──────────────────────────────────────────────

    private bool CanManagePeople()
    {
        if (IsSuperAdmin()) return true;
        var allowed = User.FindFirstValue("AllowedItemTypes") ?? "";
        return allowed.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Contains("People", StringComparer.OrdinalIgnoreCase);
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllFamilies()
    {
        if (!CanManagePeople()) return Forbid();

        // Load all active users with spouse + children; exclude users who are linked children (they appear under their parent family)
        var linkedUserIds = await db.Children
            .Where(c => c.LinkedUserId.HasValue)
            .Select(c => c.LinkedUserId!.Value)
            .ToListAsync();

        var users = await db.Users
            .Where(u => !linkedUserIds.Contains(u.Id))
            .Include(u => u.Children)
            .Include(u => u.Spouse)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();

        // Build family groups — primary user is whoever has the lower Id when spouses exist
        var visited = new HashSet<int>();
        var groups = new List<object>();

        foreach (var u in users)
        {
            if (visited.Contains(u.Id)) continue;
            visited.Add(u.Id);

            User? spouse = null;
            if (u.SpouseUserId.HasValue)
            {
                spouse = users.FirstOrDefault(s => s.Id == u.SpouseUserId.Value);
                if (spouse != null) visited.Add(spouse.Id);
            }

            // Combine children from both spouses, deduplicated
            var allChildren = u.Children
                .Concat(spouse?.Children ?? Enumerable.Empty<Child>())
                .DistinctBy(c => c.Id)
                .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
                .Select(c => new { c.Id, c.FirstName, c.LastName, BirthDate = c.BirthDate?.ToString("yyyy-MM-dd"), Gender = c.Gender == null ? (string?)null : c.Gender.ToString(), c.LinkedUserId })
                .ToList<object>();

            groups.Add(new
            {
                primaryUser = MapUser(u),
                spouse = spouse == null ? null : MapUser(spouse),
                children = allChildren,
            });
        }

        return Ok(groups);
    }

    private static object MapUser(User u) => new
    {
        u.Id,
        u.FirstName,
        u.LastName,
        u.Username,
        u.Email,
        u.IsActive,
        BirthDate = u.BirthDate?.ToString("yyyy-MM-dd"),
        Gender = u.Gender == null ? (string?)null : u.Gender.ToString(),
        MembershipStatus = u.MembershipStatus.ToString(),
        JoinedBy = u.JoinedBy?.ToString(),
        MembershipDate = u.MembershipDate?.ToString("yyyy-MM-dd"),
        u.HasLeft,
        u.IsDeceased,
    };

    [HttpDelete("admin/{userId}/children/{childId}")]
    public async Task<IActionResult> AdminRemoveChildFromFamily(int userId, int childId)
    {
        if (!CanManagePeople()) return Forbid();
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == childId && c.ParentUserId == userId);
        if (child == null) return NotFound();
        child.ParentUserId = null;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("admin/{userId}/spouse")]
    public async Task<IActionResult> AdminRemoveSpouse(int userId)
    {
        if (!CanManagePeople()) return Forbid();
        var user = await db.Users.FindAsync(userId);
        if (user == null) return NotFound();
        if (user.SpouseUserId.HasValue)
        {
            var spouse = await db.Users.FindAsync(user.SpouseUserId.Value);
            if (spouse != null) spouse.SpouseUserId = null;
            user.SpouseUserId = null;
        }
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Unclassed children (for banners on classes/attendance pages) ──────────

    [HttpGet("unclassed-children")]
    public async Task<IActionResult> GetUnclassedChildren()
    {
        var uid = CurrentUserId();
        var me = await db.Users.FindAsync(uid);
        if (me == null) return NotFound();

        // Include spouse's children too
        var parentIds = new List<int> { uid };
        if (me.SpouseUserId.HasValue) parentIds.Add(me.SpouseUserId.Value);

        var children = await db.Children
            .Where(c => c.ParentUserId.HasValue && parentIds.Contains(c.ParentUserId.Value))
            .Include(c => c.ClassChildren)
            .ToListAsync();

        var unclassed = children.Where(c => !c.ClassChildren.Any(cc => !cc.IsRemoved)).ToList();
        return Ok(unclassed.Select(c => new { c.Id, c.FirstName, c.LastName }));
    }

    // ── Admin: child link suggestions ─────────────────────────────────────────

    [HttpGet("child-link-suggestions")]
    public async Task<IActionResult> GetChildLinkSuggestions()
    {
        if (!IsSuperAdmin()) return Forbid();

        var suggestions = await db.ChildLinkSuggestions
            .Where(s => !s.IsResolved)
            .Include(s => s.RequestingUser)
            .Include(s => s.NewChild)
            .Include(s => s.SuggestedChild)
            .ToListAsync();

        return Ok(suggestions.Select(s => new
        {
            s.Id,
            requestingUser = new {
                s.RequestingUser.Id,
                s.RequestingUser.FirstName,
                s.RequestingUser.LastName,
                s.RequestingUser.Username,
            },
            newChild = new { s.NewChild.Id, s.NewChild.FirstName, s.NewChild.LastName, s.NewChild.BirthDate },
            suggestedChild = new { s.SuggestedChild.Id, s.SuggestedChild.FirstName, s.SuggestedChild.LastName, s.SuggestedChild.BirthDate },
            s.CreatedAt,
        }));
    }

    [HttpPost("child-link-suggestions/{id}/link")]
    public async Task<IActionResult> LinkChild(int id)
    {
        if (!IsSuperAdmin()) return Forbid();

        var suggestion = await db.ChildLinkSuggestions
            .Include(s => s.NewChild)
            .Include(s => s.SuggestedChild)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsResolved);
        if (suggestion == null) return NotFound();

        // Transfer the parent link to the existing (suggested) child
        suggestion.SuggestedChild.ParentUserId = suggestion.RequestingUserId;

        // Delete all suggestions referencing the new duplicate child first (Restrict FK requires this)
        var related = await db.ChildLinkSuggestions
            .Where(s => s.NewChildId == suggestion.NewChildId).ToListAsync();
        db.ChildLinkSuggestions.RemoveRange(related);
        await db.SaveChangesAsync();

        // Now safe to delete the duplicate child record
        db.Children.Remove(suggestion.NewChild);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("child-link-suggestions/{id}/dismiss")]
    public async Task<IActionResult> DismissSuggestion(int id)
    {
        if (!IsSuperAdmin()) return Forbid();

        var suggestion = await db.ChildLinkSuggestions.FindAsync(id);
        if (suggestion == null) return NotFound();
        suggestion.IsResolved = true;
        await db.SaveChangesAsync();
        return NoContent();
    }
    // ── Admin: Create Full Family ────────────────────────────────────────────────

    [HttpPost("admin/create-family")]
    public async Task<IActionResult> AdminCreateFamily([FromBody] CreateFamilyRequest req)
    {
        if (!CanManagePeople()) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Primary.FirstName) || string.IsNullOrWhiteSpace(req.Primary.LastName) || string.IsNullOrWhiteSpace(req.Primary.Email))
            return BadRequest(new { message = "Primary member: first name, last name, and email are required." });
        if (req.Spouse != null && (string.IsNullOrWhiteSpace(req.Spouse.FirstName) || string.IsNullOrWhiteSpace(req.Spouse.LastName) || string.IsNullOrWhiteSpace(req.Spouse.Email)))
            return BadRequest(new { message = "Spouse: first name, last name, and email are required." });
        if (await db.Users.AnyAsync(u => u.Email == req.Primary.Email.Trim().ToLower()))
            return Conflict(new { message = $"A user with email {req.Primary.Email} already exists." });
        if (req.Spouse != null && await db.Users.AnyAsync(u => u.Email == req.Spouse.Email.Trim().ToLower()))
            return Conflict(new { message = $"A user with email {req.Spouse.Email} already exists." });

        static string Cap(string s) { var l = new string(s.Where(char.IsLetter).ToArray()); return l.Length == 0 ? "" : char.ToUpper(l[0]) + l[1..].ToLower(); }
        async Task<string> UniqueUsername(string first, string last) {
            var b = Cap(first) + Cap(last);
            var u = b; var n = 1;
            while (await db.Users.AnyAsync(x => x.Username == u)) u = b + n++;
            return u;
        }

        var siteUrl = config["App:NextFrontendUrl"]?.Trim() ?? config["App:FrontendUrl"]?.Split(',')[0].Trim() ?? "";

        async Task<User> CreateAndInvite(NewMemberPayload p) {
            var user = new User {
                FirstName = p.FirstName.Trim(),
                LastName = p.LastName.Trim(),
                Username = await UniqueUsername(p.FirstName, p.LastName),
                Email = p.Email.Trim().ToLower(),
                Role = UserRole.Member,
                AllowedItemTypes = "",
                IsActive = false,
                BirthDate = string.IsNullOrWhiteSpace(p.BirthDate) ? null : DateOnly.Parse(p.BirthDate),
                Gender = Enum.TryParse<Gender>(p.Gender, true, out var mg) ? mg : null,
                MembershipStatus = Enum.TryParse<MembershipStatus>(p.MembershipStatus, out var ms) ? ms : MembershipStatus.NotAMember,
                JoinedBy = Enum.TryParse<JoinedBy>(p.JoinedBy, out var jb) ? jb : null,
                MembershipDate = string.IsNullOrWhiteSpace(p.MembershipDate) ? null : DateOnly.Parse(p.MembershipDate),
                HasLeft = p.HasLeft,
                IsDeceased = p.IsDeceased,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            db.InviteTokens.Add(new InviteToken { Token = token, UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddHours(48) });
            await db.SaveChangesAsync();

            var link = $"{siteUrl}/setup-password?token={token}";
            try { await email.SendInviteAsync(user.Email, user.Username, link); } catch { /* don't fail if email fails */ }

            return user;
        }

        var primary = await CreateAndInvite(req.Primary);

        User? spouse = null;
        if (req.Spouse != null) {
            spouse = await CreateAndInvite(req.Spouse);
            primary.SpouseUserId = spouse.Id;
            spouse.SpouseUserId = primary.Id;
            await db.SaveChangesAsync();
        }

        foreach (var c in req.Children ?? []) {
            var child = new Child {
                FirstName = c.FirstName.Trim(),
                LastName = c.LastName.Trim(),
                BirthDate = string.IsNullOrWhiteSpace(c.BirthDate) ? null : DateOnly.Parse(c.BirthDate),
                Gender = Enum.TryParse<Gender>(c.Gender, true, out var cg) ? cg : null,
                ParentUserId = primary.Id,
            };
            db.Children.Add(child);
        }
        await db.SaveChangesAsync();

        return Ok(new { message = "Family created.", primaryId = primary.Id, spouseId = spouse?.Id });
    }
}

public record NewMemberPayload(
    string FirstName, string LastName, string Email,
    string? BirthDate, string? Gender,
    string? MembershipStatus, string? JoinedBy, string? MembershipDate,
    bool HasLeft, bool IsDeceased
);
public record NewChildPayload(string FirstName, string LastName, string? BirthDate, string? Gender = null);
public record CreateFamilyRequest(NewMemberPayload Primary, NewMemberPayload? Spouse, List<NewChildPayload>? Children);
public record SpouseRequest(string FirstName, string LastName, string Email);
public record FamilyChildRequest(string FirstName, string LastName, string? BirthDate, string? Gender = null);

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/children")]
[Authorize]
public class ChildrenController(AppDbContext db) : ControllerBase
{
    private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");
    private bool CanManageClasses() =>
        IsSuperAdmin() ||
        (User.FindFirstValue("AllowedItemTypes") ?? "").Split(',').Contains("Classes", StringComparer.OrdinalIgnoreCase);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!CanManageClasses()) return Forbid();
        var children = await db.Children
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .ToListAsync();
        return Ok(children);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ChildPayload req)
    {
        if (!CanManageClasses()) return Forbid();
        var child = new Child
        {
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
        };
        db.Children.Add(child);
        await db.SaveChangesAsync();
        return Ok(new { child.Id, child.FirstName, child.LastName, child.CreatedAt });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsSuperAdmin()) return Forbid();
        var child = await db.Children.FindAsync(id);
        if (child == null) return NotFound();
        db.Children.Remove(child);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record ChildPayload(string FirstName, string LastName);

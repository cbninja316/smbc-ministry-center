using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GivingController(AppDbContext db) : ControllerBase
{
    // GET /api/giving/categories — income budget categories for giving dropdown
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var cats = await db.BudgetCategories
            .Where(c => c.IsIncome)
            .OrderBy(c => c.SortOrder)
            .Select(c => new { c.Id, c.Name, c.ColorHex })
            .ToListAsync();

        return Ok(cats);
    }

    // POST /api/giving — log a giving entry for the authenticated user
    [HttpPost]
    public async Task<IActionResult> Give([FromBody] GiveRequest req)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        if (req.Amount <= 0)
            return BadRequest(new { message = "Amount must be greater than zero." });

        BudgetCategory? cat = null;
        if (req.BudgetCategoryId.HasValue)
        {
            cat = await db.BudgetCategories.FindAsync(req.BudgetCategoryId.Value);
            if (cat is null || !cat.IsIncome)
                return BadRequest(new { message = "Invalid giving category." });
        }

        var entry = new GivingEntry
        {
            UserId = userId,
            BudgetCategoryId = cat?.Id,
            CategoryName = cat?.Name ?? req.CategoryName ?? "General",
            Amount = req.Amount,
            Date = req.Date ?? DateTime.UtcNow,
            Notes = req.Notes
        };

        db.GivingEntries.Add(entry);

        // Mirror into BudgetEntries so it shows in the budget income tracking
        if (cat is not null)
        {
            var user = await db.Users.FindAsync(userId);
            var budgetEntry = new BudgetEntry
            {
                BudgetCategoryId = cat.Id,
                Amount = req.Amount,
                Date = entry.Date,
                Description = $"Online giving — {user?.Username ?? "member"}",
                Notes = req.Notes
            };
            db.BudgetEntries.Add(budgetEntry);
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            entry.Id,
            entry.CategoryName,
            entry.Amount,
            Date = entry.Date.ToString("yyyy-MM-dd"),
            entry.Notes
        });
    }

    // GET /api/giving/my — authenticated user's giving history
    [HttpGet("my")]
    public async Task<IActionResult> GetMyGiving()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var entries = await db.GivingEntries
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.Date)
            .Select(g => new
            {
                g.Id,
                g.CategoryName,
                g.BudgetCategoryId,
                g.Amount,
                Date = g.Date.ToString("yyyy-MM-dd"),
                g.Notes
            })
            .ToListAsync();

        return Ok(entries);
    }
}

public record GiveRequest(decimal Amount, int? BudgetCategoryId, string? CategoryName, DateTime? Date, string? Notes);

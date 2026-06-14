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

    // GET /api/giving/my-salary — returns the current user's salary budget category + this month's remaining
    [HttpGet("my-salary")]
    public async Task<IActionResult> GetMySalary()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var cat = await db.BudgetCategories
            .FirstOrDefaultAsync(c => c.IsSalary && c.SalaryUserId == userId);

        if (cat is null) return NotFound();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var spent = await db.BudgetEntries
            .Where(e => e.BudgetCategoryId == cat.Id && e.Date >= monthStart && e.Date < monthEnd)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;

        var monthly = cat.AllocatedAmount;
        var remaining = Math.Max(0, monthly - spent);

        return Ok(new
        {
            cat.Id,
            cat.Name,
            MonthlyAmount = monthly,
            CurrentMonthSpent = spent,
            Remaining = remaining
        });
    }

    // POST /api/giving/donate-check — log salary donation: creates giving entry + salary budget entry
    [HttpPost("donate-check")]
    public async Task<IActionResult> DonateCheck([FromBody] DonateCheckRequest req)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        if (req.Amount <= 0)
            return BadRequest(new { message = "Amount must be greater than zero." });

        // Verify the user has a salary category
        var salaryCat = await db.BudgetCategories
            .FirstOrDefaultAsync(c => c.IsSalary && c.SalaryUserId == userId);
        if (salaryCat is null)
            return BadRequest(new { message = "No salary category found for your account." });

        // Verify the giving (income) category
        BudgetCategory? givingCat = null;
        if (req.GivingCategoryId.HasValue)
        {
            givingCat = await db.BudgetCategories.FindAsync(req.GivingCategoryId.Value);
            if (givingCat is null || !givingCat.IsIncome)
                return BadRequest(new { message = "Invalid giving category." });
        }

        var date = req.Date ?? DateTime.UtcNow;
        var user = await db.Users.FindAsync(userId);

        // Giving entry (shows in member's giving history / tax record)
        var givingEntry = new GivingEntry
        {
            UserId = userId,
            BudgetCategoryId = givingCat?.Id,
            CategoryName = givingCat?.Name ?? "General",
            Amount = req.Amount,
            Date = date,
            Notes = req.Notes ?? $"Salary donation — {salaryCat.Name}"
        };
        db.GivingEntries.Add(givingEntry);

        // Mirror giving into income budget tracking
        if (givingCat is not null)
        {
            db.BudgetEntries.Add(new BudgetEntry
            {
                BudgetCategoryId = givingCat.Id,
                Amount = req.Amount,
                Date = date,
                Description = $"Salary donation — {user?.Username ?? "member"}",
                Notes = req.Notes
            });
        }

        // Budget entry on the salary expense line (zeroes it out)
        db.BudgetEntries.Add(new BudgetEntry
        {
            BudgetCategoryId = salaryCat.Id,
            Amount = req.Amount,
            Date = date,
            Description = $"Check donated — {user?.Username ?? "member"}",
            Notes = req.Notes
        });

        await db.SaveChangesAsync();

        return Ok(new
        {
            givingEntry.Id,
            givingEntry.CategoryName,
            givingEntry.Amount,
            Date = givingEntry.Date.ToString("yyyy-MM-dd"),
            givingEntry.Notes
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
public record DonateCheckRequest(decimal Amount, int? GivingCategoryId, DateTime? Date, string? Notes);

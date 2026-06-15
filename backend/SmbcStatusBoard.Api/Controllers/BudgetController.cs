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
public class BudgetController(AppDbContext db) : ControllerBase
{
    // ── Categories ────────────────────────────────────────────────────────────

    bool IsSuperAdmin() => User.FindFirstValue("role") == "SuperAdmin" ||
        User.IsInRole("SuperAdmin") ||
        (User.FindFirstValue(ClaimTypes.Role) == "SuperAdmin");

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var superAdmin = IsSuperAdmin();
        var cats = await db.BudgetCategories
            .Include(c => c.SalaryUser)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.TypeName).ThenBy(c => c.Name)
            .ToListAsync();

        return Ok(cats.Select(c => new
        {
            c.Id, c.TypeName, c.Name, c.IsIncome, c.ColorHex, c.SortOrder, c.IsSalary,
            // Non-super-admins see zero amounts for salary categories
            allocatedAmount       = (c.IsSalary && !superAdmin) ? 0m : c.AllocatedAmount,
            yearlyAllocatedAmount = (c.IsSalary && !superAdmin) ? 0m : c.YearlyAllocatedAmount,
            // Only super admins see the linked user
            salaryUserId   = superAdmin ? c.SalaryUserId   : (int?)null,
            salaryUserName = superAdmin ? (c.SalaryUser != null ? $"{c.SalaryUser.FirstName} {c.SalaryUser.LastName}".Trim() : null) : null,
            c.PeriodStartMonth, c.PeriodStartDay, c.PeriodEndMonth, c.PeriodEndDay,
        }));
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryRequest req)
    {
        if (!IsSuperAdmin()) return Forbid();
        var cat = new BudgetCategory
        {
            TypeName = req.TypeName,
            Name = req.Name,
            AllocatedAmount = req.AllocatedAmount,
            YearlyAllocatedAmount = req.YearlyAllocatedAmount,
            IsIncome = req.IsIncome,
            ColorHex = req.ColorHex ?? "#3B82F6",
            SortOrder = await db.BudgetCategories.CountAsync(),
            IsSalary = req.IsSalary,
            SalaryUserId = req.IsSalary ? req.SalaryUserId : null,
            PeriodStartMonth = req.PeriodStartMonth,
            PeriodStartDay   = req.PeriodStartDay,
            PeriodEndMonth   = req.PeriodEndMonth,
            PeriodEndDay     = req.PeriodEndDay,
        };
        db.BudgetCategories.Add(cat);
        await db.SaveChangesAsync();
        return Ok(cat);
    }

    [HttpPut("categories/{id}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryRequest req)
    {
        if (!IsSuperAdmin()) return Forbid();
        var cat = await db.BudgetCategories.FindAsync(id);
        if (cat is null) return NotFound();
        cat.TypeName = req.TypeName;
        cat.Name = req.Name;
        cat.AllocatedAmount = req.AllocatedAmount;
        cat.YearlyAllocatedAmount = req.YearlyAllocatedAmount;
        cat.IsIncome = req.IsIncome;
        cat.ColorHex = req.ColorHex ?? cat.ColorHex;
        cat.IsSalary = req.IsSalary;
        cat.SalaryUserId = req.IsSalary ? req.SalaryUserId : null;
        cat.PeriodStartMonth = req.PeriodStartMonth;
        cat.PeriodStartDay   = req.PeriodStartDay;
        cat.PeriodEndMonth   = req.PeriodEndMonth;
        cat.PeriodEndDay     = req.PeriodEndDay;
        await db.SaveChangesAsync();
        return Ok(cat);
    }

    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var cat = await db.BudgetCategories.FindAsync(id);
        if (cat is null) return NotFound();
        db.BudgetCategories.Remove(cat);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("categories/reorder")]
    public async Task<IActionResult> ReorderCategories([FromBody] List<int> orderedIds)
    {
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var cat = await db.BudgetCategories.FindAsync(orderedIds[i]);
            if (cat is not null) cat.SortOrder = i;
        }
        await db.SaveChangesAsync();
        return Ok();
    }

    // ── Entries ───────────────────────────────────────────────────────────────

    [HttpGet("entries")]
    public async Task<IActionResult> GetEntries([FromQuery] int year, [FromQuery] int month)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        var entries = await db.BudgetEntries
            .Include(e => e.Category)
            .Where(e => e.Date >= start && e.Date < end)
            .OrderByDescending(e => e.Date)
            .ToListAsync();
        return Ok(entries.Select(e => MapEntry(e)));
    }

    [HttpPost("entries")]
    public async Task<IActionResult> CreateEntry([FromBody] EntryRequest req)
    {
        var entry = new BudgetEntry
        {
            BudgetCategoryId = req.BudgetCategoryId,
            Amount = req.Amount,
            Date = req.Date.ToUniversalTime(),
            Description = req.Description,
            Notes = req.Notes,
            ReceiptId = req.ReceiptId,
        };
        db.BudgetEntries.Add(entry);
        await db.SaveChangesAsync();
        await db.Entry(entry).Reference(e => e.Category).LoadAsync();

        // Auto-donate: if this entry is for a salary category and the linked user has auto-donate on
        if (entry.Category.IsSalary && entry.Category.SalaryUserId.HasValue)
        {
            var salaryUser = await db.Users
                .Include(u => u.SalaryDonateGivingCategory)
                .FirstOrDefaultAsync(u => u.Id == entry.Category.SalaryUserId.Value);

            if (salaryUser is { SalaryDonateEnabled: true, SalaryDonatePercentage: > 0 }
                && salaryUser.SalaryDonateGivingCategoryId.HasValue)
            {
                var givingCat = salaryUser.SalaryDonateGivingCategory;
                var donateAmount = Math.Round(entry.Amount * salaryUser.SalaryDonatePercentage / 100m, 2);
                if (donateAmount > 0 && givingCat is not null)
                {
                    db.GivingEntries.Add(new GivingEntry
                    {
                        UserId = salaryUser.Id,
                        BudgetCategoryId = givingCat.Id,
                        CategoryName = givingCat.Name,
                        Amount = donateAmount,
                        Date = entry.Date,
                        Notes = $"Auto-donation {salaryUser.SalaryDonatePercentage}% of salary"
                    });
                    db.BudgetEntries.Add(new BudgetEntry
                    {
                        BudgetCategoryId = givingCat.Id,
                        Amount = donateAmount,
                        Date = entry.Date,
                        Description = $"Salary donation — {salaryUser.Username}",
                    });
                    await db.SaveChangesAsync();
                }
            }
        }

        return Ok(MapEntry(entry));
    }

    [HttpPut("entries/{id}")]
    public async Task<IActionResult> UpdateEntry(int id, [FromBody] EntryRequest req)
    {
        var entry = await db.BudgetEntries.Include(e => e.Category).FirstOrDefaultAsync(e => e.Id == id);
        if (entry is null) return NotFound();
        entry.BudgetCategoryId = req.BudgetCategoryId;
        entry.Amount = req.Amount;
        entry.Date = req.Date.ToUniversalTime();
        entry.Description = req.Description;
        entry.Notes = req.Notes;
        await db.SaveChangesAsync();
        await db.Entry(entry).Reference(e => e.Category).LoadAsync();
        return Ok(MapEntry(entry));
    }

    [HttpDelete("entries/{id}")]
    public async Task<IActionResult> DeleteEntry(int id)
    {
        var entry = await db.BudgetEntries.FindAsync(id);
        if (entry is null) return NotFound();
        db.BudgetEntries.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Receipt assignment ────────────────────────────────────────────────────

    /// <summary>Returns receipts that haven't been assigned to a budget entry yet and not dismissed</summary>
    [HttpGet("unassigned-receipts")]
    public async Task<IActionResult> GetUnassignedReceipts()
    {
        var assignedReceiptIds = await db.BudgetEntries
            .Where(e => e.ReceiptId.HasValue)
            .Select(e => e.ReceiptId!.Value)
            .ToListAsync();

        var receipts = await db.Receipts
            .Where(r => !r.BudgetDismissed && !assignedReceiptIds.Contains(r.Id))
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .ToListAsync();

        return Ok(receipts);
    }

    [HttpPost("receipts/{id}/dismiss")]
    public async Task<IActionResult> DismissReceipt(int id)
    {
        var receipt = await db.Receipts.FindAsync(id);
        if (receipt is null) return NotFound();
        receipt.BudgetDismissed = true;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("receipts/{id}/assign")]
    public async Task<IActionResult> AssignReceipt(int id, [FromBody] AssignReceiptRequest req)
    {
        var receipt = await db.Receipts.FindAsync(id);
        if (receipt is null) return NotFound();

        var entry = new BudgetEntry
        {
            BudgetCategoryId = req.BudgetCategoryId,
            Amount = receipt.Amount,
            Date = receipt.Date.ToUniversalTime(),
            Description = receipt.Description,
            Notes = req.Notes,
            ReceiptId = id,
        };
        db.BudgetEntries.Add(entry);
        await db.SaveChangesAsync();
        await db.Entry(entry).Reference(e => e.Category).LoadAsync();
        return Ok(MapEntry(entry));
    }

    // ── Summary (for graph) ───────────────────────────────────────────────────

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] int year, [FromQuery] int month)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var today = DateTime.UtcNow;
        var dayOfMonth = (today.Year == year && today.Month == month)
            ? today.Day
            : (today < start ? 0 : daysInMonth);

        var categories = await db.BudgetCategories.ToListAsync();
        var entries = await db.BudgetEntries
            .Where(e => e.Date >= start && e.Date < end)
            .ToListAsync();

        var totalAllocatedExpense = categories.Where(c => !c.IsIncome).Sum(c => c.AllocatedAmount);
        var totalAllocatedIncome  = categories.Where(c => c.IsIncome).Sum(c => c.AllocatedAmount);
        var totalSpentExpense = entries
            .Where(e => !categories.FirstOrDefault(c => c.Id == e.BudgetCategoryId)?.IsIncome ?? false)
            .Sum(e => e.Amount);
        var totalSpentIncome = entries
            .Where(e => categories.FirstOrDefault(c => c.Id == e.BudgetCategoryId)?.IsIncome ?? false)
            .Sum(e => e.Amount);

        // Build daily cumulative expense spending for the graph
        var dailyCumulative = new decimal[daysInMonth + 1]; // index 1..daysInMonth
        foreach (var entry in entries)
        {
            var entryDay = entry.Date.Day;
            var cat = categories.FirstOrDefault(c => c.Id == entry.BudgetCategoryId);
            if (cat is not null && !cat.IsIncome && entryDay >= 1 && entryDay <= daysInMonth)
                dailyCumulative[entryDay] += entry.Amount;
        }
        // Make it cumulative
        for (var d = 2; d <= daysInMonth; d++)
            dailyCumulative[d] += dailyCumulative[d - 1];

        var dailyPoints = Enumerable.Range(1, daysInMonth)
            .Select(d => new { day = d, amount = dailyCumulative[d] })
            .ToList();

        // Per-category breakdown
        var isSuperAdminSummary = IsSuperAdmin();
        var allYearEntries = await db.BudgetEntries
            .Where(e => e.Date.Year == year && categories.Select(c => c.Id).Contains(e.BudgetCategoryId))
            .ToListAsync();
        var categoryBreakdown = categories.Select(c =>
        {
            var spent = entries.Where(e => e.BudgetCategoryId == c.Id).Sum(e => e.Amount);
            var ytdSpent = allYearEntries.Where(e => e.BudgetCategoryId == c.Id).Sum(e => e.Amount);
            var redact = c.IsSalary && !isSuperAdminSummary;
            var yearlyAlloc = c.YearlyAllocatedAmount > 0 ? c.YearlyAllocatedAmount : c.AllocatedAmount * 12;

            // Periodic category: treat as yearly-only; allocatedAmount = 0 when outside active window
            var isPeriodic = c.PeriodStartMonth.HasValue && c.PeriodStartDay.HasValue
                          && c.PeriodEndMonth.HasValue   && c.PeriodEndDay.HasValue;
            var effectiveMonthly = c.AllocatedAmount;
            if (isPeriodic)
            {
                var periodStart = new DateOnly(year, c.PeriodStartMonth!.Value, c.PeriodStartDay!.Value);
                var periodEnd   = new DateOnly(year, c.PeriodEndMonth!.Value,   c.PeriodEndDay!.Value);
                var monthDate   = new DateOnly(year, month, 1);
                var monthEnd    = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
                // Active if any day of the current month overlaps the period
                effectiveMonthly = (monthDate <= periodEnd && monthEnd >= periodStart)
                    ? c.YearlyAllocatedAmount   // show the lump sum when in-period
                    : 0m;
            }

            return new
            {
                c.Id,
                c.TypeName,
                c.Name,
                c.IsIncome,
                c.ColorHex,
                c.IsSalary,
                AllocatedAmount = redact ? 0m : effectiveMonthly,
                YearlyAllocatedAmount = redact ? 0m : yearlyAlloc,
                Spent = spent,
                YtdSpent = ytdSpent,
                c.PeriodStartMonth, c.PeriodStartDay, c.PeriodEndMonth, c.PeriodEndDay,
            };
        }).ToList();

        return Ok(new
        {
            year,
            month,
            daysInMonth,
            dayOfMonth,
            totalAllocatedExpense,
            totalAllocatedIncome,
            totalSpentExpense,
            totalSpentIncome,
            dailyPoints,
            categoryBreakdown,
        });
    }

    // ── Yearly summary (for annual graph) ────────────────────────────────────

    [HttpGet("yearly-summary")]
    public async Task<IActionResult> GetYearlySummary([FromQuery] int year)
    {
        var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd   = new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var categories = await db.BudgetCategories.ToListAsync();
        var entries    = await db.BudgetEntries
            .Where(e => e.Date >= yearStart && e.Date < yearEnd)
            .ToListAsync();

        var today      = DateTime.UtcNow;
        var currentMonth = (today.Year == year) ? today.Month : (today.Year > year ? 12 : 0);

        // Per-month actual spending (expenses) and income received
        var monthlyExpense = new decimal[13]; // index 1-12
        var monthlyIncome  = new decimal[13];
        foreach (var entry in entries)
        {
            var m = entry.Date.Month;
            var cat = categories.FirstOrDefault(c => c.Id == entry.BudgetCategoryId);
            if (cat is null) continue;
            if (cat.IsIncome) monthlyIncome[m]  += entry.Amount;
            else              monthlyExpense[m]  += entry.Amount;
        }

        // Yearly allocation per category (explicit override or monthly * 12)
        static decimal YearlyAlloc(BudgetCategory c) =>
            c.YearlyAllocatedAmount > 0 ? c.YearlyAllocatedAmount : c.AllocatedAmount * 12;

        var totalYearlyExpense = categories.Where(c => !c.IsIncome).Sum(YearlyAlloc);
        var totalYearlyIncome  = categories.Where(c => c.IsIncome).Sum(YearlyAlloc);

        // Build monthly points for graph
        var monthlyPoints = Enumerable.Range(1, 12).Select(m => new
        {
            month = m,
            expenseActual  = monthlyExpense[m],
            incomeActual   = monthlyIncome[m],
            expenseProjected = totalYearlyExpense / 12,   // even monthly pace
            incomeProjected  = totalYearlyIncome  / 12,
        }).ToList();

        // Cumulative actual (for cumulative line graph)
        var cumExpense = 0m;
        var cumulativePoints = monthlyPoints.Select(p =>
        {
            cumExpense += p.expenseActual;
            return new { p.month, cumulativeExpense = cumExpense, projectedCumulative = p.expenseProjected * p.month };
        }).ToList();

        // Per-category yearly breakdown
        var categoryBreakdown = categories.Select(c =>
        {
            var spent = entries.Where(e => e.BudgetCategoryId == c.Id).Sum(e => e.Amount);
            return new
            {
                c.Id, c.TypeName, c.Name, c.ColorHex, c.IsIncome,
                YearlyAllocated = YearlyAlloc(c),
                Spent = spent,
            };
        }).ToList();

        return Ok(new
        {
            year,
            currentMonth,
            totalYearlyExpense,
            totalYearlyIncome,
            totalSpentExpenseYtd = monthlyExpense.Sum(),
            totalSpentIncomeYtd  = monthlyIncome.Sum(),
            monthlyPoints,
            cumulativePoints,
            categoryBreakdown,
        });
    }

    // ── Monthly breakdown (per-category spending by month for a whole year) ──

    [HttpGet("monthly-breakdown")]
    public async Task<IActionResult> GetMonthlyBreakdown([FromQuery] int year)
    {
        var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd   = new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var categories = await db.BudgetCategories
            .OrderBy(c => c.SortOrder).ThenBy(c => c.TypeName).ThenBy(c => c.Name)
            .ToListAsync();
        var entries = await db.BudgetEntries
            .Where(e => e.Date >= yearStart && e.Date < yearEnd)
            .ToListAsync();

        static decimal YearlyAlloc(BudgetCategory c) =>
            c.YearlyAllocatedAmount > 0 ? c.YearlyAllocatedAmount : c.AllocatedAmount * 12;

        var superAdmin = IsSuperAdmin();
        var categoryData = categories.Select(c =>
        {
            var monthlySpent = new decimal[13]; // 1-indexed; index 0 unused
            foreach (var e in entries.Where(e => e.BudgetCategoryId == c.Id))
                monthlySpent[e.Date.Month] += e.Amount;

            var redact = c.IsSalary && !superAdmin;
            return new
            {
                c.Id,
                c.TypeName,
                c.Name,
                c.IsIncome,
                c.ColorHex,
                c.IsSalary,
                AllocatedAmount       = redact ? 0m : c.AllocatedAmount,
                YearlyAllocatedAmount = redact ? 0m : YearlyAlloc(c),
                // 12 elements, index 0 = Jan, index 11 = Dec
                MonthlySpent = Enumerable.Range(1, 12).Select(m => monthlySpent[m]).ToArray(),
                YtdSpent = monthlySpent.Skip(1).Sum(),
            };
        }).ToList();

        // Monthly income/expense totals across all categories
        var monthlyIncomeTotals  = Enumerable.Range(1, 12).Select(m =>
            entries.Where(e => categories.Any(c => c.Id == e.BudgetCategoryId && c.IsIncome) &&
                               e.Date.Month == m).Sum(e => e.Amount)).ToArray();
        var monthlyExpenseTotals = Enumerable.Range(1, 12).Select(m =>
            entries.Where(e => categories.Any(c => c.Id == e.BudgetCategoryId && !c.IsIncome) &&
                               e.Date.Month == m).Sum(e => e.Amount)).ToArray();

        return Ok(new
        {
            year,
            categories = categoryData,
            monthlyIncomeTotals,
            monthlyExpenseTotals,
            ytdIncome  = monthlyIncomeTotals.Sum(),
            ytdExpense = monthlyExpenseTotals.Sum(),
            totalYearlyIncome  = categories.Where(c => c.IsIncome).Sum(c => (!superAdmin && c.IsSalary) ? 0m : YearlyAlloc(c)),
            totalYearlyExpense = categories.Where(c => !c.IsIncome).Sum(c => (!superAdmin && c.IsSalary) ? 0m : YearlyAlloc(c)),
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static object MapEntry(BudgetEntry e) => new
    {
        e.Id,
        e.BudgetCategoryId,
        categoryName = e.Category?.Name,
        typeName = e.Category?.TypeName,
        colorHex = e.Category?.ColorHex,
        e.Amount,
        date = e.Date.ToString("yyyy-MM-dd"),
        e.Description,
        e.Notes,
        e.ReceiptId,
    };

    // ── Request DTOs ──────────────────────────────────────────────────────────

    public record CategoryRequest(
        string TypeName,
        string Name,
        decimal AllocatedAmount,
        decimal YearlyAllocatedAmount,
        bool IsIncome,
        string? ColorHex,
        bool IsSalary = false,
        int? SalaryUserId = null,
        int? PeriodStartMonth = null,
        int? PeriodStartDay   = null,
        int? PeriodEndMonth   = null,
        int? PeriodEndDay     = null
    );

    public record EntryRequest(
        int BudgetCategoryId,
        decimal Amount,
        DateTime Date,
        string Description,
        string? Notes,
        int? ReceiptId
    );

    public record AssignReceiptRequest(
        int BudgetCategoryId,
        string? Notes
    );
}

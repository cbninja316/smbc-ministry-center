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

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories() =>
        Ok(await db.BudgetCategories.OrderBy(c => c.SortOrder).ThenBy(c => c.TypeName).ThenBy(c => c.Name).ToListAsync());

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryRequest req)
    {
        var cat = new BudgetCategory
        {
            TypeName = req.TypeName,
            Name = req.Name,
            AllocatedAmount = req.AllocatedAmount,
            IsIncome = req.IsIncome,
            ColorHex = req.ColorHex ?? "#3B82F6",
            SortOrder = await db.BudgetCategories.CountAsync(),
        };
        db.BudgetCategories.Add(cat);
        await db.SaveChangesAsync();
        return Ok(cat);
    }

    [HttpPut("categories/{id}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryRequest req)
    {
        var cat = await db.BudgetCategories.FindAsync(id);
        if (cat is null) return NotFound();
        cat.TypeName = req.TypeName;
        cat.Name = req.Name;
        cat.AllocatedAmount = req.AllocatedAmount;
        cat.IsIncome = req.IsIncome;
        cat.ColorHex = req.ColorHex ?? cat.ColorHex;
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
        var categoryBreakdown = categories.Select(c =>
        {
            var spent = entries.Where(e => e.BudgetCategoryId == c.Id).Sum(e => e.Amount);
            return new
            {
                c.Id,
                c.TypeName,
                c.Name,
                c.AllocatedAmount,
                c.IsIncome,
                c.ColorHex,
                Spent = spent,
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
        bool IsIncome,
        string? ColorHex
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

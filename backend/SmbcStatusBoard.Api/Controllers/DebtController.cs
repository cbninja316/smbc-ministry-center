using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/debts")]
[Authorize]
public class DebtController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var debts = await db.Debts
            .Include(d => d.Payments)
            .OrderBy(d => d.Name)
            .ToListAsync();
        return Ok(debts.Select(MapDebt));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOne(int id)
    {
        var debt = await db.Debts.Include(d => d.Payments).FirstOrDefaultAsync(d => d.Id == id);
        if (debt is null) return NotFound();
        return Ok(MapDebt(debt));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DebtRequest req)
    {
        var debt = new Debt
        {
            Name = req.Name,
            PrincipalAmount = req.PrincipalAmount,
            InterestRate = req.InterestRate,
            LoanTermMonths = req.LoanTermMonths,
            MonthsIn = req.MonthsIn,
            DueDate = req.DueDate,
            Notes = req.Notes,
            BudgetCategoryId = req.BudgetCategoryId,
        };
        db.Debts.Add(debt);
        await db.SaveChangesAsync();
        await db.Entry(debt).Collection(d => d.Payments).LoadAsync();
        await SyncDebtEntries(debt);
        return Ok(MapDebt(debt));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] DebtRequest req)
    {
        var debt = await db.Debts.Include(d => d.Payments).FirstOrDefaultAsync(d => d.Id == id);
        if (debt is null) return NotFound();
        debt.Name = req.Name;
        debt.PrincipalAmount = req.PrincipalAmount;
        debt.InterestRate = req.InterestRate;
        debt.LoanTermMonths = req.LoanTermMonths;
        debt.MonthsIn = req.MonthsIn;
        debt.DueDate = req.DueDate;
        debt.Notes = req.Notes;
        debt.BudgetCategoryId = req.BudgetCategoryId;
        await db.SaveChangesAsync();
        await SyncDebtEntries(debt);
        return Ok(MapDebt(debt));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var debt = await db.Debts.FindAsync(id);
        if (debt is null) return NotFound();
        // Remove auto-generated entries before deleting the debt
        var entries = await db.BudgetEntries.Where(e => e.DebtId == id).ToListAsync();
        db.BudgetEntries.RemoveRange(entries);
        db.Debts.Remove(debt);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Payments (extra principal) ───────────────────────────────────────────

    [HttpPost("{id}/payments")]
    public async Task<IActionResult> AddPayment(int id, [FromBody] PaymentRequest req)
    {
        var debt = await db.Debts.FindAsync(id);
        if (debt is null) return NotFound();
        var payment = new DebtPayment
        {
            DebtId = id,
            ExtraPrincipal = req.ExtraPrincipal,
            Date = req.Date,
            Notes = req.Notes,
        };
        db.DebtPayments.Add(payment);
        await db.SaveChangesAsync();
        return Ok(MapPayment(payment));
    }

    [HttpDelete("{id}/payments/{paymentId}")]
    public async Task<IActionResult> DeletePayment(int id, int paymentId)
    {
        var payment = await db.DebtPayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.DebtId == id);
        if (payment is null) return NotFound();
        db.DebtPayments.Remove(payment);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Entry sync ───────────────────────────────────────────────────────────

    private async Task SyncDebtEntries(Debt debt)
    {
        // Remove all previously auto-generated entries for this debt
        var old = await db.BudgetEntries.Where(e => e.DebtId == debt.Id).ToListAsync();
        db.BudgetEntries.RemoveRange(old);

        if (debt.BudgetCategoryId is null || debt.MonthsIn <= 0)
        {
            await db.SaveChangesAsync();
            return;
        }

        var monthly = CalcMonthlyPayment(debt.PrincipalAmount, debt.InterestRate, debt.LoanTermMonths);

        // Determine the day of month to use for entries
        int dueDay = 1;
        if (!string.IsNullOrEmpty(debt.DueDate) && DateTime.TryParse(debt.DueDate, out var dueDt))
            dueDay = dueDt.Day;

        // Loan start = today minus monthsIn months
        var today = DateTime.Today;
        var loanStart = today.AddMonths(-debt.MonthsIn);

        for (int n = 1; n <= debt.MonthsIn; n++)
        {
            var paymentMonth = loanStart.AddMonths(n);
            var day = Math.Min(dueDay, DateTime.DaysInMonth(paymentMonth.Year, paymentMonth.Month));
            var entryDate = new DateTime(paymentMonth.Year, paymentMonth.Month, day);

            db.BudgetEntries.Add(new BudgetEntry
            {
                BudgetCategoryId = debt.BudgetCategoryId.Value,
                DebtId = debt.Id,
                Amount = Math.Round(monthly, 2),
                Date = entryDate,
                Description = $"{debt.Name} — Payment #{n}",
            });
        }

        await db.SaveChangesAsync();
    }

    private static decimal CalcMonthlyPayment(decimal principal, decimal annualRate, int termMonths)
    {
        if (annualRate == 0 || termMonths == 0) return termMonths > 0 ? principal / termMonths : 0;
        var r = (double)(annualRate / 100m / 12m);
        var n = termMonths;
        var pmt = (double)principal * r * Math.Pow(1 + r, n) / (Math.Pow(1 + r, n) - 1);
        return (decimal)pmt;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static object MapDebt(Debt d) => new
    {
        d.Id,
        d.Name,
        d.PrincipalAmount,
        d.InterestRate,
        d.LoanTermMonths,
        d.MonthsIn,
        d.DueDate,
        d.Notes,
        d.CreatedAt,
        d.BudgetCategoryId,
        Payments = d.Payments.OrderBy(p => p.Date).Select(MapPayment),
    };

    private static object MapPayment(DebtPayment p) => new
    {
        p.Id,
        p.DebtId,
        p.ExtraPrincipal,
        p.Date,
        p.Notes,
    };

    public record DebtRequest(
        string Name,
        decimal PrincipalAmount,
        decimal InterestRate,
        int LoanTermMonths,
        int MonthsIn,
        string? DueDate,
        string? Notes,
        int? BudgetCategoryId
    );

    public record PaymentRequest(
        decimal ExtraPrincipal,
        string Date,
        string? Notes
    );
}

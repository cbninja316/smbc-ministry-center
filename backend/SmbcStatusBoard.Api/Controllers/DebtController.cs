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
        };
        db.Debts.Add(debt);
        await db.SaveChangesAsync();
        await db.Entry(debt).Collection(d => d.Payments).LoadAsync();
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
        await db.SaveChangesAsync();
        return Ok(MapDebt(debt));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var debt = await db.Debts.FindAsync(id);
        if (debt is null) return NotFound();
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
        Payments = d.Payments.Select(MapPayment).OrderBy(p => ((dynamic)p).date),
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
        string? Notes
    );

    public record PaymentRequest(
        decimal ExtraPrincipal,
        string Date,
        string? Notes
    );
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BankAccountController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.BankAccounts.OrderBy(a => a.Name).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BankAccountRequest req)
    {
        var account = new BankAccount
        {
            Name = req.Name,
            Balance = req.Balance,
            Notes = req.Notes,
            LastUpdatedAt = DateTime.UtcNow,
        };
        db.BankAccounts.Add(account);
        await db.SaveChangesAsync();
        return Ok(account);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] BankAccountRequest req)
    {
        var account = await db.BankAccounts.FindAsync(id);
        if (account is null) return NotFound();
        account.Name = req.Name;
        account.Balance = req.Balance;
        account.Notes = req.Notes;
        account.LastUpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(account);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var account = await db.BankAccounts.FindAsync(id);
        if (account is null) return NotFound();
        db.BankAccounts.Remove(account);
        await db.SaveChangesAsync();
        return NoContent();
    }

    public record BankAccountRequest(string Name, decimal Balance, string? Notes);
}

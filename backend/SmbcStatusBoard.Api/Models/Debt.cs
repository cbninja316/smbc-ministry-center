namespace SmbcStatusBoard.Api.Models;

public class Debt
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PrincipalAmount { get; set; }
    public decimal InterestRate { get; set; }   // annual %, e.g. 6.5
    public int LoanTermMonths { get; set; }      // total length of loan
    public int MonthsIn { get; set; }            // how many months have already elapsed
    public string? DueDate { get; set; }         // ISO date — monthly payment due date (e.g. "2026-07-01")
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? BudgetCategoryId { get; set; }
    public BudgetCategory? BudgetCategory { get; set; }

    public ICollection<DebtPayment> Payments { get; set; } = [];
}

namespace SmbcStatusBoard.Api.Models;

public class BudgetCategoryAmountHistory
{
    public int Id { get; set; }
    public int BudgetCategoryId { get; set; }
    public BudgetCategory Category { get; set; } = null!;

    /// <summary>Year/Month this amount takes effect (inclusive, forward).</summary>
    public int Year  { get; set; }
    public int Month { get; set; }

    public decimal AllocatedAmount { get; set; }
}

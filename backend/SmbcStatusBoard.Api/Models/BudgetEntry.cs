namespace SmbcStatusBoard.Api.Models;

public class BudgetEntry
{
    public int Id { get; set; }

    public int BudgetCategoryId { get; set; }
    public BudgetCategory Category { get; set; } = null!;

    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Notes { get; set; }

    /// <summary>If this entry was created from a receipt, links back to it</summary>
    public int? ReceiptId { get; set; }
    public Receipt? Receipt { get; set; }
}

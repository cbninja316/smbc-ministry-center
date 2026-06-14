namespace SmbcStatusBoard.Api.Models;

public class GivingEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string CategoryName { get; set; } = string.Empty; // snapshot of income category name
    public int? BudgetCategoryId { get; set; }              // FK to BudgetCategory (nullable for flexibility)
    public BudgetCategory? BudgetCategory { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

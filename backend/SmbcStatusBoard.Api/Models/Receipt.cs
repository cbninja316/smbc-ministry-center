namespace SmbcStatusBoard.Api.Models;

public class Receipt
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Ministry { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string SubmittedBy { get; set; } = string.Empty;
    public string OneDriveFileId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDone { get; set; } = false;

    /// <summary>User dismissed the "assign to budget" notification for this receipt</summary>
    public bool BudgetDismissed { get; set; } = false;
}

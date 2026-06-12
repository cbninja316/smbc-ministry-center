namespace SmbcStatusBoard.Api.Models;

public class BankAccount
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;        // e.g. "General Checking"
    public decimal Balance { get; set; }
    public string? Notes { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}

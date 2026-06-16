namespace SmbcStatusBoard.Api.Models;

public class DebtPayment
{
    public int Id { get; set; }
    public int DebtId { get; set; }
    public Debt Debt { get; set; } = null!;

    public decimal ExtraPrincipal { get; set; }
    public string Date { get; set; } = string.Empty;   // ISO date "yyyy-MM-dd"
    public string? Notes { get; set; }
}

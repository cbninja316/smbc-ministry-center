namespace SmbcStatusBoard.Api.Models;

public class BudgetTypeOrder
{
    public int Id { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

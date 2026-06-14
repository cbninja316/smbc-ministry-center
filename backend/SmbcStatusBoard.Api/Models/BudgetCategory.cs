namespace SmbcStatusBoard.Api.Models;

public class BudgetCategory
{
    public int Id { get; set; }

    /// <summary>High-level grouping, e.g. "Ministries", "Facilities", "Income"</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Specific category, e.g. "Youth Ministry", "Utilities"</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Monthly allocation amount</summary>
    public decimal AllocatedAmount { get; set; }

    /// <summary>
    /// Optional annual override. If 0, the yearly budget is computed as AllocatedAmount * 12.
    /// Use this for categories with non-uniform annual spend (e.g. annual insurance).
    /// </summary>
    public decimal YearlyAllocatedAmount { get; set; } = 0;

    /// <summary>Whether this is an income category (vs expense)</summary>
    public bool IsIncome { get; set; } = false;

    /// <summary>Hex color for display, e.g. "#3B82F6"</summary>
    public string ColorHex { get; set; } = "#3B82F6";

    public int SortOrder { get; set; } = 0;

    /// <summary>If true, this category represents a staff salary line.</summary>
    public bool IsSalary { get; set; } = false;

    /// <summary>Optional user tied to this salary line. Only visible to SuperAdmin.</summary>
    public int? SalaryUserId { get; set; }
    public User? SalaryUser { get; set; }

    public ICollection<BudgetEntry> Entries { get; set; } = new List<BudgetEntry>();
}

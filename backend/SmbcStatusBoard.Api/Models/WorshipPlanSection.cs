namespace SmbcStatusBoard.Api.Models;

public class WorshipPlanSection
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public WorshipPlan Plan { get; set; } = null!;
    public string Title { get; set; } = "";
    public int Order { get; set; }

    public ICollection<WorshipPlanItem> Items { get; set; } = [];
}

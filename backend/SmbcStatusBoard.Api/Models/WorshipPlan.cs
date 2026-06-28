namespace SmbcStatusBoard.Api.Models;

public class WorshipPlan
{
    public int Id { get; set; }
    public int ServiceTypeId { get; set; }
    public WorshipServiceType ServiceType { get; set; } = null!;
    public DateOnly PlanDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorshipPlanSection> Sections { get; set; } = [];
}

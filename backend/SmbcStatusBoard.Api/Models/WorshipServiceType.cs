namespace SmbcStatusBoard.Api.Models;

public class WorshipServiceType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    // JSON: [{title: string, order: number}]
    public string SectionsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorshipPlan> Plans { get; set; } = [];
    public ICollection<VolunteerRole> VolunteerRoles { get; set; } = [];
}

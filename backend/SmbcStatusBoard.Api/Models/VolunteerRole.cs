namespace SmbcStatusBoard.Api.Models;
public class VolunteerRole {
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
    public int? SpecialEventId { get; set; }          // null = Sunday role; set = special-event role
    public SpecialEvent? SpecialEvent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<VolunteerAssignment> Assignments { get; set; } = new List<VolunteerAssignment>();
}

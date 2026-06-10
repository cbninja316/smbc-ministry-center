namespace SmbcStatusBoard.Api.Models;

public enum RecurrenceType { None, Weekly, Monthly, Yearly }

public class SpecialEvent
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public RecurrenceType Recurrence { get; set; } = RecurrenceType.None;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

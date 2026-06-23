namespace SmbcStatusBoard.Api.Models;

public enum ClassType { Adult, Children }

public class Class
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DayOfWeek { get; set; }  // 0=Sunday … 6=Saturday
    public string ClassTime { get; set; } = string.Empty;  // e.g. "9:00 AM"
    public ClassType Type { get; set; }
    public int? PromotionClassId { get; set; }
    public Class? PromotionClass { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ClassMember> Members { get; set; } = [];
    public ICollection<ClassChild> ClassChildren { get; set; } = [];
    public ICollection<ClassAttendance> Attendance { get; set; } = [];
    public ICollection<ChildAttendance> ChildAttendance { get; set; } = [];
}

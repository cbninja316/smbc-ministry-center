namespace SmbcStatusBoard.Api.Models;

public class ClassAttendance
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateOnly SessionDate { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

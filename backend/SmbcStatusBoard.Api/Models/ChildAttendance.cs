namespace SmbcStatusBoard.Api.Models;

public class ChildAttendance
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public int ChildId { get; set; }
    public Child Child { get; set; } = null!;
    public DateOnly SessionDate { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

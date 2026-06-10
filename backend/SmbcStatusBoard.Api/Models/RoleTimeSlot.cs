namespace SmbcStatusBoard.Api.Models;

public class RoleTimeSlot
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public VolunteerRole Role { get; set; } = null!;
    public string Time { get; set; } = string.Empty;   // e.g. "9:00 AM"
    public string Label { get; set; } = string.Empty;  // e.g. "Worship Set"
    public int SortOrder { get; set; } = 0;
}

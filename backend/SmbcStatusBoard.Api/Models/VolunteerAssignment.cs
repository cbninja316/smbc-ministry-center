namespace SmbcStatusBoard.Api.Models;
public enum AssignmentStatus { Pending, Accepted, Rejected }
public class VolunteerAssignment {
    public int Id { get; set; }
    public int RoleId { get; set; }
    public VolunteerRole Role { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime SundayDate { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Pending;
    public string ResponseToken { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

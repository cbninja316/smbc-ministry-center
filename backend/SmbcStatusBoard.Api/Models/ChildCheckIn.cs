namespace SmbcStatusBoard.Api.Models;

public class ChildCheckIn
{
    public int Id { get; set; }
    public int ChildId { get; set; }
    public Child Child { get; set; } = null!;
    public DateTime CheckedInAt { get; set; } = DateTime.UtcNow;
    public DateTime? CheckedOutAt { get; set; }
    public int CheckedInByUserId { get; set; }
    public User CheckedInByUser { get; set; } = null!;
    public int? CheckedOutByUserId { get; set; }
    public User? CheckedOutByUser { get; set; }
    public bool IsManual { get; set; } = false;
    public string? Notes { get; set; }
}

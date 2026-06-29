namespace SmbcStatusBoard.Api.Models;

public class ScheduledSms
{
    public int Id { get; set; }
    public int ChurchId { get; set; }
    public Church Church { get; set; } = null!;
    public string ContactNumber { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime ScheduledFor { get; set; }
    public bool Sent { get; set; }
    public string? TwilioSid { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

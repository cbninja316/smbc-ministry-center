namespace SmbcStatusBoard.Api.Models;

public class SmsMessage
{
    public int Id { get; set; }
    public int ChurchId { get; set; }
    public Church Church { get; set; } = null!;
    public string ContactNumber { get; set; } = string.Empty;  // E.164
    public string? ContactName { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string Body { get; set; } = string.Empty;
    public string Direction { get; set; } = "outbound";  // "outbound" | "inbound"
    public string Status { get; set; } = "sent";  // sent | delivered | failed | received
    public string? TwilioSid { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

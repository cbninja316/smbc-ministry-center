namespace SmbcStatusBoard.Api.Models;

public class TwilioSettings
{
    public int Id { get; set; }
    public int ChurchId { get; set; }
    public Church Church { get; set; } = null!;
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;  // stored as-is (SQLite, private server)
    public string FromNumber { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

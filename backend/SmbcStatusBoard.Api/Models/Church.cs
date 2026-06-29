namespace SmbcStatusBoard.Api.Models;

public class Church
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LogoData { get; set; }       // base64 data URL
    public string? Slug { get; set; }            // url-friendly identifier (future subdomain use)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
}

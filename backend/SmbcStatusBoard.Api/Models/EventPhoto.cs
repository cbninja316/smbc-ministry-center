namespace SmbcStatusBoard.Api.Models;

public class EventPhoto
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsHeroImage { get; set; } = false;
}

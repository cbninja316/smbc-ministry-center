namespace SmbcStatusBoard.Api.Models;

public class WorshipSong
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public int? DurationSeconds { get; set; }
    public string? PraiseChartsId { get; set; }
    public string? PraiseChartsSlug { get; set; }
    public string? PraiseChartsThumbnailUrl { get; set; }
    public string FilesJson { get; set; } = "[]";  // [{name, path, type, size}]
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

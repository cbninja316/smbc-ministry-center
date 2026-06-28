namespace SmbcStatusBoard.Api.Models;

public class WorshipPlanItem
{
    public int Id { get; set; }
    public int SectionId { get; set; }
    public WorshipPlanSection Section { get; set; } = null!;
    public int? SongId { get; set; }
    public WorshipSong? Song { get; set; }
    public string? EventTitle { get; set; }
    public string? LeaderName { get; set; }
    public int? DurationSeconds { get; set; }
    public int Order { get; set; }
}

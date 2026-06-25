namespace SmbcStatusBoard.Api.Models;

public class SpecialEventTimeSlot
{
    public int Id { get; set; }
    public int SpecialEventId { get; set; }
    public SpecialEvent SpecialEvent { get; set; } = null!;
    public string Time { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

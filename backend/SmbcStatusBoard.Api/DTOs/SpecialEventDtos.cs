namespace SmbcStatusBoard.Api.DTOs;

public record CreateSpecialEventRequest(
    string Label,
    string Description,
    DateTime StartDate,
    DateTime? EndDate,
    string Recurrence
);

public class SpecialEventResponse
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string? EndDate { get; set; }
    public string Recurrence { get; set; } = "None";
}

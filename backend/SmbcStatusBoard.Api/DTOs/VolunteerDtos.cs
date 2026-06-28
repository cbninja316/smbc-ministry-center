using SmbcStatusBoard.Api.Models;
namespace SmbcStatusBoard.Api.DTOs;

public record TimeSlotRequest(string Time, string Label, int SortOrder = 0);
public record CreateRoleRequest(string Label, string Description, int? SpecialEventId = null, List<TimeSlotRequest>? TimeSlots = null, int? WorshipServiceTypeId = null);
public record UpdateRoleRequest(string Label, string Description, List<TimeSlotRequest>? TimeSlots = null, int? WorshipServiceTypeId = null);
public record CreateAssignmentRequest(int RoleId, int UserId, DateTime SundayDate);

public class TimeSlotResponse
{
    public int Id { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class VolunteerRoleResponse
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int? SpecialEventId { get; set; }
    public int? WorshipServiceTypeId { get; set; }
    public string? WorshipServiceTypeName { get; set; }
    public List<TimeSlotResponse> TimeSlots { get; set; } = new();
}

public class VolunteerAssignmentResponse
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public string RoleLabel { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public DateTime SundayDate { get; set; }
    public string Status { get; set; } = "Pending";
}

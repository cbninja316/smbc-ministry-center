using SmbcStatusBoard.Api.Models;
namespace SmbcStatusBoard.Api.DTOs;
public record CreateRoleRequest(string Label, string Description, int? SpecialEventId = null);
public record CreateAssignmentRequest(int RoleId, int UserId, DateTime SundayDate);
public class VolunteerRoleResponse {
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int? SpecialEventId { get; set; }
}
public class VolunteerAssignmentResponse {
    public int Id { get; set; }
    public int RoleId { get; set; }
    public string RoleLabel { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public DateTime SundayDate { get; set; }
    public string Status { get; set; } = "Pending";
}

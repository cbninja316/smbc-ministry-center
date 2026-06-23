namespace SmbcStatusBoard.Api.Models;

public class ClassMember
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public Class Class { get; set; } = null!;

    // Existing user (SuperAdmin/Admin/Member added directly)
    public int? UserId { get; set; }
    public User? User { get; set; }

    // Pending invite for a non-user (adult class external invite)
    public string? InviteEmail { get; set; }
    public string? InviteFirstName { get; set; }
    public string? InviteLastName { get; set; }

    public string Status { get; set; } = "Active";  // Active | Pending
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

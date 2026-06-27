namespace SmbcStatusBoard.Api.Models;

public class Child
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public Gender? Gender { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? ParentUserId { get; set; }
    public User? ParentUser { get; set; }

    public int? LinkedUserId { get; set; }
    public User? LinkedUser { get; set; }

    // Check-in verification
    public bool IsVerified { get; set; } = false;
    public DateTime? VerifiedAt { get; set; }
    public int? VerifiedByUserId { get; set; }
    public User? VerifiedByUser { get; set; }
    public string? CheckInToken { get; set; }

    public ICollection<ClassChild> ClassChildren { get; set; } = [];
}

namespace SmbcStatusBoard.Api.Models;

public enum UserRole { Member, Admin, SuperAdmin }

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Admin;
    public bool IsActive { get; set; } = false;
    public bool EmailVerified { get; set; } = false;
    public string AllowedItemTypes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateOnly? BirthDate { get; set; }

    public string? PreferencesJson { get; set; }

    public ICollection<InviteToken> InviteTokens { get; set; } = new List<InviteToken>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
    public ICollection<EmailVerificationToken> EmailVerificationTokens { get; set; } = new List<EmailVerificationToken>();
    public ICollection<GivingEntry> GivingEntries { get; set; } = new List<GivingEntry>();
}

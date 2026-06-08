namespace SmbcStatusBoard.Api.Models;

public enum UserRole { Admin, SuperAdmin }

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Admin;
    public bool IsActive { get; set; } = false;
    public string AllowedItemTypes { get; set; } = string.Empty; // comma-separated
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? PreferencesJson { get; set; }

    public ICollection<InviteToken> InviteTokens { get; set; } = new List<InviteToken>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
}

namespace SmbcStatusBoard.Api.Models;

public enum UserRole { Member, Admin, SuperAdmin }
public enum MembershipStatus { NotAMember, Member }
public enum JoinedBy { TransferByLetter, AcceptedChrist, StatementOfFaith }
public enum Gender { Male, Female }

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
    public Gender? Gender { get; set; }

    // Church membership
    public MembershipStatus MembershipStatus { get; set; } = MembershipStatus.NotAMember;
    public JoinedBy? JoinedBy { get; set; }
    public DateOnly? MembershipDate { get; set; }
    public bool HasLeft { get; set; } = false;
    public bool IsDeceased { get; set; } = false;

    public bool SalaryDonateEnabled { get; set; } = false;
    public decimal SalaryDonatePercentage { get; set; } = 0;
    public int? SalaryDonateGivingCategoryId { get; set; }
    public BudgetCategory? SalaryDonateGivingCategory { get; set; }

    public string? PreferencesJson { get; set; }

    public string? StripeCustomerId { get; set; }

    // Family
    public int? SpouseUserId { get; set; }
    public User? Spouse { get; set; }

    public ICollection<Child> Children { get; set; } = new List<Child>();
    public ICollection<InviteToken> InviteTokens { get; set; } = new List<InviteToken>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
    public ICollection<EmailVerificationToken> EmailVerificationTokens { get; set; } = new List<EmailVerificationToken>();
    public ICollection<GivingEntry> GivingEntries { get; set; } = new List<GivingEntry>();
}

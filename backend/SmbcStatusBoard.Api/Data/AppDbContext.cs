using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Models;

namespace SmbcStatusBoard.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<InviteToken> InviteTokens => Set<InviteToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EventPhoto> EventPhotos => Set<EventPhoto>();
    public DbSet<VolunteerRole> VolunteerRoles => Set<VolunteerRole>();
    public DbSet<VolunteerAssignment> VolunteerAssignments => Set<VolunteerAssignment>();
    public DbSet<RoleTimeSlot> RoleTimeSlots => Set<RoleTimeSlot>();
    public DbSet<SpecialEvent> SpecialEvents => Set<SpecialEvent>();
    public DbSet<BudgetCategory> BudgetCategories => Set<BudgetCategory>();
    public DbSet<BudgetEntry> BudgetEntries => Set<BudgetEntry>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<GivingEntry> GivingEntries => Set<GivingEntry>();
    public DbSet<BudgetCategoryAmountHistory> BudgetCategoryAmountHistories => Set<BudgetCategoryAmountHistory>();
    public DbSet<BudgetTypeOrder> BudgetTypeOrders => Set<BudgetTypeOrder>();
    public DbSet<Debt> Debts => Set<Debt>();
    public DbSet<DebtPayment> DebtPayments => Set<DebtPayment>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<ClassMember> ClassMembers => Set<ClassMember>();
    public DbSet<Child> Children => Set<Child>();
    public DbSet<ClassChild> ClassChildren => Set<ClassChild>();
    public DbSet<ClassAttendance> ClassAttendances => Set<ClassAttendance>();
    public DbSet<ChildAttendance> ChildAttendances => Set<ChildAttendance>();
    public DbSet<ChildLinkSuggestion> ChildLinkSuggestions => Set<ChildLinkSuggestion>();
    public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();
    public DbSet<SpecialEventTimeSlot> SpecialEventTimeSlots => Set<SpecialEventTimeSlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasOne(u => u.SalaryDonateGivingCategory)
            .WithMany()
            .HasForeignKey(u => u.SalaryDonateGivingCategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<InviteToken>()
            .HasOne(t => t.User)
            .WithMany(u => u.InviteTokens)
            .HasForeignKey(t => t.UserId);

        modelBuilder.Entity<PasswordResetToken>()
            .HasOne(t => t.User)
            .WithMany(u => u.PasswordResetTokens)
            .HasForeignKey(t => t.UserId);

        modelBuilder.Entity<EventPhoto>()
            .HasOne(p => p.Item)
            .WithMany(i => i.EventPhotos)
            .HasForeignKey(p => p.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Receipt>()
            .Property(r => r.Amount)
            .HasColumnType("TEXT");

        modelBuilder.Entity<BudgetCategory>()
            .Property(c => c.AllocatedAmount)
            .HasColumnType("TEXT");

        modelBuilder.Entity<BudgetCategory>()
            .Property(c => c.YearlyAllocatedAmount)
            .HasColumnType("TEXT");

        modelBuilder.Entity<BudgetCategory>()
            .HasOne(c => c.SalaryUser)
            .WithMany()
            .HasForeignKey(c => c.SalaryUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<BankAccount>()
            .Property(a => a.Balance)
            .HasColumnType("TEXT");

        modelBuilder.Entity<BudgetEntry>()
            .Property(e => e.Amount)
            .HasColumnType("TEXT");

        modelBuilder.Entity<BudgetEntry>()
            .HasOne(e => e.Category)
            .WithMany(c => c.Entries)
            .HasForeignKey(e => e.BudgetCategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BudgetEntry>()
            .HasOne(e => e.Receipt)
            .WithMany()
            .HasForeignKey(e => e.ReceiptId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<VolunteerAssignment>()
            .HasOne(a => a.Role)
            .WithMany(r => r.Assignments)
            .HasForeignKey(a => a.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<VolunteerAssignment>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<VolunteerAssignment>()
            .HasIndex(a => a.ResponseToken)
            .IsUnique();

        modelBuilder.Entity<VolunteerRole>()
            .HasOne(r => r.SpecialEvent)
            .WithMany()
            .HasForeignKey(r => r.SpecialEventId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RoleTimeSlot>()
            .HasOne(t => t.Role)
            .WithMany(r => r.TimeSlots)
            .HasForeignKey(t => t.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EmailVerificationToken>()
            .HasOne(t => t.User)
            .WithMany(u => u.EmailVerificationTokens)
            .HasForeignKey(t => t.UserId);

        modelBuilder.Entity<GivingEntry>()
            .HasOne(g => g.User)
            .WithMany(u => u.GivingEntries)
            .HasForeignKey(g => g.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GivingEntry>()
            .HasOne(g => g.BudgetCategory)
            .WithMany()
            .HasForeignKey(g => g.BudgetCategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<GivingEntry>()
            .Property(g => g.Amount)
            .HasColumnType("TEXT");

        modelBuilder.Entity<BudgetCategoryAmountHistory>()
            .HasOne(h => h.Category)
            .WithMany()
            .HasForeignKey(h => h.BudgetCategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BudgetCategoryAmountHistory>()
            .HasIndex(h => new { h.BudgetCategoryId, h.Year, h.Month })
            .IsUnique();

        modelBuilder.Entity<BudgetCategoryAmountHistory>()
            .Property(h => h.AllocatedAmount)
            .HasColumnType("TEXT");

        modelBuilder.Entity<BudgetTypeOrder>()
            .HasIndex(t => t.TypeName)
            .IsUnique();

        modelBuilder.Entity<Debt>()
            .Property(d => d.PrincipalAmount)
            .HasColumnType("TEXT");

        modelBuilder.Entity<Debt>()
            .Property(d => d.InterestRate)
            .HasColumnType("TEXT");

        modelBuilder.Entity<Debt>()
            .HasOne(d => d.BudgetCategory)
            .WithMany()
            .HasForeignKey(d => d.BudgetCategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<DebtPayment>()
            .HasOne(p => p.Debt)
            .WithMany(d => d.Payments)
            .HasForeignKey(p => p.DebtId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DebtPayment>()
            .Property(p => p.ExtraPrincipal)
            .HasColumnType("TEXT");

        modelBuilder.Entity<BudgetEntry>()
            .HasOne(e => e.Debt)
            .WithMany()
            .HasForeignKey(e => e.DebtId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AppSetting>()
            .HasKey(s => s.Key);

        modelBuilder.Entity<Class>()
            .HasOne(c => c.PromotionClass)
            .WithMany()
            .HasForeignKey(c => c.PromotionClassId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ClassMember>()
            .HasOne(m => m.Class)
            .WithMany(c => c.Members)
            .HasForeignKey(m => m.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ClassMember>()
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ClassChild>()
            .HasOne(cc => cc.Class)
            .WithMany(c => c.ClassChildren)
            .HasForeignKey(cc => cc.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ClassChild>()
            .HasOne(cc => cc.Child)
            .WithMany(c => c.ClassChildren)
            .HasForeignKey(cc => cc.ChildId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ClassAttendance>()
            .HasOne(a => a.Class)
            .WithMany(c => c.Attendance)
            .HasForeignKey(a => a.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ClassAttendance>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ClassAttendance>()
            .HasIndex(a => new { a.ClassId, a.UserId, a.SessionDate })
            .IsUnique();

        modelBuilder.Entity<ChildAttendance>()
            .HasOne(a => a.Class)
            .WithMany(c => c.ChildAttendance)
            .HasForeignKey(a => a.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChildAttendance>()
            .HasOne(a => a.Child)
            .WithMany()
            .HasForeignKey(a => a.ChildId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChildAttendance>()
            .HasIndex(a => new { a.ClassId, a.ChildId, a.SessionDate })
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasOne(u => u.Spouse)
            .WithMany()
            .HasForeignKey(u => u.SpouseUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Child>()
            .HasOne(c => c.ParentUser)
            .WithMany(u => u.Children)
            .HasForeignKey(c => c.ParentUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Child>()
            .HasOne(c => c.LinkedUser)
            .WithMany()
            .HasForeignKey(c => c.LinkedUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ChildLinkSuggestion>()
            .HasOne(s => s.RequestingUser)
            .WithMany()
            .HasForeignKey(s => s.RequestingUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EventRegistration>()
            .HasOne(r => r.Child)
            .WithMany()
            .HasForeignKey(r => r.ChildId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChildLinkSuggestion>()
            .HasOne(s => s.NewChild)
            .WithMany()
            .HasForeignKey(s => s.NewChildId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChildLinkSuggestion>()
            .HasOne(s => s.SuggestedChild)
            .WithMany()
            .HasForeignKey(s => s.SuggestedChildId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

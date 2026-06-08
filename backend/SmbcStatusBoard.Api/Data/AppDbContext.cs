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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<InviteToken>()
            .HasOne(t => t.User)
            .WithMany(u => u.InviteTokens)
            .HasForeignKey(t => t.UserId);

        modelBuilder.Entity<PasswordResetToken>()
            .HasOne(t => t.User)
            .WithMany(u => u.PasswordResetTokens)
            .HasForeignKey(t => t.UserId);

        modelBuilder.Entity<Receipt>()
            .Property(r => r.Amount)
            .HasColumnType("TEXT");
    }
}

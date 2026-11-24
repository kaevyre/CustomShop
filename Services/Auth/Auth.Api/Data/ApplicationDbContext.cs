using Auth.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Data;

/// <summary>Створює стандартні таблиці Identity (AspNetUsers, AspNetRoles, AspNetUserRoles, ...) + RefreshTokens.</summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
            e.Property(t => t.CreatedByIp).HasMaxLength(64);
            e.Property(t => t.RevokedReason).HasMaxLength(256);
            e.Property(t => t.ReplacedByTokenHash).HasMaxLength(128);
            e.Ignore(t => t.IsRevoked).Ignore(t => t.IsExpired).Ignore(t => t.IsActive);
            e.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

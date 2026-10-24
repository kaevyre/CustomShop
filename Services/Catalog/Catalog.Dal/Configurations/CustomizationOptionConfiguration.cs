using Catalog.Dal.Seeding;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class CustomizationOptionConfiguration : IEntityTypeConfiguration<CustomizationOption>
{
    public void Configure(EntityTypeBuilder<CustomizationOption> builder)
    {
        builder.ToTable("CustomizationOptions", t =>
            t.HasCheckConstraint("CK_CustomizationOptions_PriceDelta", "[PriceDelta] >= 0"));

        builder.HasKey(o => o.Id);
        builder.Property(o => o.GroupName).IsRequired().HasMaxLength(100);
        builder.Property(o => o.Value).IsRequired().HasMaxLength(200);
        builder.Property(o => o.PriceDelta).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(o => o.IsDefault).HasDefaultValue(false);
        builder.Property(o => o.IsAvailable).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(o => o.SortOrder).HasDefaultValue(0);

        // одне значення не повторюється в групі товару
        builder.HasIndex(o => new { o.ProductId, o.GroupName, o.Value }).IsUnique();

        // у групі не більше одного значення за замовчуванням
        builder.HasIndex(o => new { o.ProductId, o.GroupName })
            .IsUnique()
            .HasFilter("[IsDefault] = 1")
            .HasDatabaseName("IX_CustomizationOptions_DefaultPerGroup");

        builder.HasData(SeedData.Options);
    }
}

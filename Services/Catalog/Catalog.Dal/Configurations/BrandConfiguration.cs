using Catalog.Dal.Seeding;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands", t =>
            t.HasCheckConstraint("CK_Brands_Website", "[Website] IS NULL OR [Website] LIKE N'https://%'"));

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Name).IsRequired().HasMaxLength(100);
        builder.Property(b => b.Country).HasMaxLength(60);
        builder.Property(b => b.Website).HasMaxLength(200);

        builder.HasIndex(b => b.Name).IsUnique();

        builder.ConfigureAudit();

        // статичний сідінг довідника з фіксованими Id
        builder.HasData(SeedData.Brands);
    }
}

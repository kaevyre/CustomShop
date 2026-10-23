using Catalog.Dal.Seeding;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", t =>
            t.HasCheckConstraint("CK_Categories_Slug", "[Slug] NOT LIKE N'%[^a-z0-9-]%'"));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(100);
        builder.Property(c => c.SortOrder).HasDefaultValue(0);

        builder.HasIndex(c => c.Slug).IsUnique();

        builder.ConfigureAudit();

        builder.HasData(SeedData.Categories);
    }
}

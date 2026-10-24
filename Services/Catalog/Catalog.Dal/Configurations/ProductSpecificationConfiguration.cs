using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class ProductSpecificationConfiguration : IEntityTypeConfiguration<ProductSpecification>
{
    public void Configure(EntityTypeBuilder<ProductSpecification> builder)
    {
        builder.ToTable("ProductSpecifications", t =>
        {
            t.HasCheckConstraint("CK_ProductSpecifications_Weight", "[WeightGrams] > 0");
            t.HasCheckConstraint("CK_ProductSpecifications_Connection",
                "[ConnectionType] IN (N'Wired', N'Wireless', N'Bluetooth', N'Hybrid', N'None')");
            t.HasCheckConstraint("CK_ProductSpecifications_Warranty", "[WarrantyMonths] BETWEEN 0 AND 60");
        });

        builder.HasKey(s => s.ProductId);                         // PK = FK → 1:1
        builder.Property(s => s.ProductId).ValueGeneratedNever();
        builder.Property(s => s.Dimensions).HasMaxLength(50);
        builder.Property(s => s.ConnectionType).IsRequired().HasMaxLength(20);
        builder.Property(s => s.Material).HasMaxLength(100);
        builder.Property(s => s.Color).HasMaxLength(50);
        builder.Property(s => s.WarrantyMonths).HasDefaultValue(12).ValueGeneratedNever();
    }
}

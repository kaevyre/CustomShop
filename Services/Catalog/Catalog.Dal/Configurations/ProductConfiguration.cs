using Catalog.Dal.Seeding;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", t =>
        {
            t.HasCheckConstraint("CK_Products_Slug", "[Slug] NOT LIKE N'%[^a-z0-9-]%'");
            t.HasCheckConstraint("CK_Products_ProductType",
                "[ProductType] IN (N'Keyboard', N'Mouse', N'Headphones', N'Speakers', N'Mousepad', N'Accessory')");
            t.HasCheckConstraint("CK_Products_BasePrice", "[BasePrice] > 0");
            t.HasCheckConstraint("CK_Products_Stock", "[Stock] >= 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Sku).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Slug).IsRequired().HasMaxLength(200);
        builder.Property(p => p.ProductType).IsRequired().HasMaxLength(20);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.BasePrice).HasColumnType("decimal(18,2)");
        builder.Property(p => p.Stock).HasDefaultValue(0);
        builder.Property(p => p.IsCustomizable).HasDefaultValue(false);
        // DEFAULT 1 лишається в БД, але EF завжди передає значення: інакше false (значення CLR за замовчуванням) підмінився б на true
        builder.Property(p => p.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(p => p.RowVersion).IsRowVersion().IsRequired();

        builder.HasIndex(p => p.Sku).IsUnique();
        builder.HasIndex(p => p.Slug).IsUnique();

        // вітрина: «усі клавіатури, від дешевших» — лише активні, не видалені
        builder.HasIndex(p => new { p.ProductType, p.BasePrice })
            .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0")
            .IncludeProperties(p => new { p.Name, p.Slug, p.BrandId, p.IsCustomizable });

        // пошук за початком назви
        builder.HasIndex(p => p.Name)
            .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0")
            .IncludeProperties(p => new { p.Slug, p.BasePrice });

        builder.HasOne(p => p.Brand)                              // N:1, без каскаду
            .WithMany()
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Specification)                      // 1:1
            .WithOne()
            .HasForeignKey<ProductSpecification>(s => s.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Options)                           // 1:N
            .WithOne()
            .HasForeignKey(o => o.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Options).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.ProductCategories).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.ConfigureAudit();

        builder.HasData(SeedData.Products);
    }
}

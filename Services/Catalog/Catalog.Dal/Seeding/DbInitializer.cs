using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Catalog.Dal.Seeding;

/// <summary>
/// Програмний сідінг після MigrateAsync. Ідемпотентний: перед вставкою перевіряє, які записи вже є,
/// тож повторний запуск нічого не дублює. Довідники вже створила міграція (HasData), тому порядок FK дотримано.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(CatalogDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var existingSpecifications = await db.ProductSpecifications
            .Select(s => s.ProductId)
            .ToListAsync(ct);

        var newSpecifications = SeedData.Specifications()
            .Where(s => !existingSpecifications.Contains(s.ProductId))
            .ToList();
        db.ProductSpecifications.AddRange(newSpecifications);

        var existingLinks = await db.ProductCategories
            .IgnoreQueryFilters()
            .Select(pc => new { pc.ProductId, pc.CategoryId })
            .ToListAsync(ct);

        var newLinks = SeedData.ProductCategories()
            .Where(l => !existingLinks.Contains(new { l.ProductId, l.CategoryId }))
            .ToList();
        db.ProductCategories.AddRange(newLinks);

        var added = await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Catalog seeding finished: {SpecificationsAdded} specifications, {LinksAdded} category links ({RowsAdded} rows)",
            newSpecifications.Count, newLinks.Count, added);
    }
}

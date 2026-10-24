using System.Globalization;
using Catalog.Domain.Entities;

namespace Catalog.Dal.Seeding;

/// <summary>
/// Початкові дані — ті самі, що в db/p2/seed.sql Лаб. 01.
/// Довідники з фіксованими Id (Brands, Categories, Products, CustomizationOptions) потрапляють у міграцію через HasData:
/// ці Id копіюють інші контексти (Products.Id → Замовлення, Відгуки; CustomizationOptions.Id → OrderItemOptions.OptionId).
/// Залежні записи без власного IDENTITY (специфікації, зв'язки з категоріями) додає DbInitializer після MigrateAsync.
/// </summary>
internal static class SeedData
{
    private static readonly DateTime SeededAt = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private const string SeededBy = "seed";

    public static readonly object[] Brands =
    [
        new { Id = 1, Name = "Keyforge", Country = "Україна", Website = "https://keyforge.example.com", CreatedAt = SeededAt, CreatedBy = SeededBy, IsDeleted = false },
        new { Id = 2, Name = "Glidex", Country = "Польща", Website = "https://glidex.example.com", CreatedAt = SeededAt, CreatedBy = SeededBy, IsDeleted = false },
        new { Id = 3, Name = "SonicCraft", Country = "Україна", Website = "https://soniccraft.example.com", CreatedAt = SeededAt, CreatedBy = SeededBy, IsDeleted = false },
        new { Id = 4, Name = "PadArt", Country = "Україна", Website = (string?)null, CreatedAt = SeededAt, CreatedBy = SeededBy, IsDeleted = false }
    ];

    public static readonly object[] Categories =
    [
        Category(1, "Клавіатури", "keyboards", 1),
        Category(2, "Миші", "mice", 2),
        Category(3, "Аудіо", "audio", 3),
        Category(4, "Килимки", "mousepads", 4),
        Category(5, "Аксесуари", "accessories", 5),
        Category(6, "Кастомізація", "custom", 6),
        Category(7, "Новинки", "new", 7)
    ];

    public static readonly object[] Products =
    [
        Product(1, 1, "KB-075-ALU", "Клавіатура Aurora 75 (алюміній)", "aurora-75-alu", "Keyboard", "Механічна клавіатура 75% в алюмінієвому корпусі, gasket mount", 4500.00m, 15, true),
        Product(2, 2, "MS-GLD-01", "Миша Glide Pro", "glide-pro", "Mouse", "Легка ігрова миша 58 г, сенсор 26K DPI", 2200.00m, 30, true),
        Product(3, 3, "HP-STU-02", "Навушники Studio Wave", "studio-wave", "Headphones", "Закриті студійні навушники зі змінними амбушурами", 3800.00m, 12, true),
        Product(4, 3, "SP-DSK-20", "Колонки DeskBeat 2.0", "deskbeat-20", "Speakers", "Настільна акустика 2.0, 2×15 Вт", 3100.00m, 10, false),
        Product(5, 4, "MP-XL-900", "Килимок Canvas XL 900×400", "canvas-xl-900", "Mousepad", "Тканинний килимок з прошитим краєм, можливий власний принт", 950.00m, 50, true),
        Product(6, 1, "KC-PBT-RETRO", "Набір кейкапів PBT Retro", "pbt-retro-keycaps", "Accessory", "Набір 140 кейкапів PBT, профіль Cherry", 1600.00m, 25, false),
        Product(7, 1, "CB-COIL-USBC", "Кабель Coil USB-C", "coil-usb-c", "Accessory", "Кручений кабель з авіаційним роз'ємом", 700.00m, 40, true),
        Product(8, 1, "KB-060-HS", "Клавіатура Pocket 60 Hot-Swap", "pocket-60-hs", "Keyboard", "Компактна 60% клавіатура з hot-swap роз'ємами", 2900.00m, 20, true)
    ];

    public static readonly object[] Options =
    [
        Option(1, 1, "Світчі", "Gateron Red", 0.00m, true, 1),
        Option(2, 1, "Світчі", "Holy Panda", 900.00m, false, 2),
        Option(3, 1, "Кейкапи", "PBT Black", 0.00m, true, 1),
        Option(4, 1, "Кейкапи", "Retro Beige", 600.00m, false, 2),
        Option(5, 1, "Колір корпусу", "Silver", 0.00m, true, 1),
        Option(6, 1, "Колір корпусу", "Midnight Blue", 400.00m, false, 2),
        Option(7, 2, "Колір корпусу", "Black", 0.00m, true, 1),
        Option(8, 2, "Колір корпусу", "Lilac", 200.00m, false, 2),
        Option(9, 2, "Покриття", "Matte", 0.00m, true, 1),
        Option(10, 2, "Покриття", "Glossy", 150.00m, false, 2),
        Option(11, 3, "Амбушури", "Velour", 0.00m, true, 1),
        Option(12, 3, "Амбушури", "Leather", 450.00m, false, 2),
        Option(13, 3, "Гравіювання", "Так", 300.00m, false, 1),
        Option(14, 5, "Принт", "Без принту", 0.00m, true, 1),
        Option(15, 5, "Принт", "Власний принт", 350.00m, false, 2),
        Option(16, 8, "Світчі", "Gateron Yellow", 0.00m, true, 1),
        Option(17, 8, "Світчі", "Kailh Box White", 500.00m, false, 2),
        Option(18, 7, "Довжина", "1.5 м", 0.00m, true, 1),
        Option(19, 7, "Довжина", "2 м", 100.00m, false, 2)
    ];

    public static IReadOnlyList<ProductSpecification> Specifications() =>
    [
        new(1, "Hybrid", 1650, "325×140×38 мм", "Алюміній", "Silver", 24),
        new(2, "Wireless", 58, "125×64×39 мм", "Пластик ABS", "Black", 24),
        new(3, "Wired", 290, "190×170×85 мм", "Пластик, метал", "Black", 12),
        new(4, "Bluetooth", 2100, "150×120×190 мм", "МДФ", "Walnut", 12),
        new(5, "None", 540, "900×400×4 мм", "Тканина, гума", "Black", 6),
        new(6, "None", 480, null, "PBT", "Beige", 6),
        new(7, "Wired", 90, "1.5 м", "Нейлонове обплетення", "Black", 12),
        new(8, "Wired", 780, "295×105×35 мм", "Пластик", "White", 12)
    ];

    public static IReadOnlyList<ProductCategory> ProductCategories() =>
    [
        Link(1, 1, "2026-06-01", true), Link(1, 6, "2026-06-01", true),
        Link(8, 1, "2026-09-15", false), Link(8, 7, "2026-09-15", true),
        Link(2, 2, "2026-06-01", true), Link(2, 6, "2026-07-10", false),
        Link(3, 3, "2026-06-01", false), Link(3, 6, "2026-08-01", false),
        Link(4, 3, "2026-06-01", false),
        Link(5, 4, "2026-06-01", true), Link(5, 6, "2026-06-01", true),
        Link(6, 5, "2026-06-20", false),
        Link(7, 5, "2026-09-20", false), Link(7, 7, "2026-09-20", false)
    ];

    private static object Category(int id, string name, string slug, int sortOrder) =>
        new { Id = id, Name = name, Slug = slug, SortOrder = sortOrder, CreatedAt = SeededAt, CreatedBy = SeededBy, IsDeleted = false };

    private static object Product(long id, int brandId, string sku, string name, string slug, string productType,
        string description, decimal basePrice, int stock, bool isCustomizable) =>
        new
        {
            Id = id, BrandId = brandId, Sku = sku, Name = name, Slug = slug, ProductType = productType,
            Description = description, BasePrice = basePrice, Stock = stock, IsCustomizable = isCustomizable,
            IsActive = true, CreatedAt = SeededAt, CreatedBy = SeededBy, IsDeleted = false
        };

    private static object Option(long id, long productId, string group, string value, decimal priceDelta,
        bool isDefault, int sortOrder) =>
        new
        {
            Id = id, ProductId = productId, GroupName = group, Value = value, PriceDelta = priceDelta,
            IsDefault = isDefault, IsAvailable = true, SortOrder = sortOrder
        };

    private static ProductCategory Link(long productId, int categoryId, string addedAt, bool isFeatured) =>
        new(productId, categoryId,
            DateTime.SpecifyKind(DateTime.Parse(addedAt, CultureInfo.InvariantCulture), DateTimeKind.Utc), isFeatured);
}

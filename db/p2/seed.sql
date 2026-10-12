-- Проєкт №2: довідникові дані. Ідемпотентний: кожна вставка перевіряє наявність (WHERE NOT EXISTS).
-- Id фіксовані, бо їх копіюють інші контексти (Products.Id, CustomizationOptions.Id).

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE CustomShop_Catalog;
GO
SET NOCOUNT ON;

SET IDENTITY_INSERT dbo.Brands ON;
INSERT dbo.Brands (Id, Name, Country, Website, CreatedBy)
SELECT s.Id, s.Name, s.Country, s.Website, N'seed'
FROM (VALUES
    (1, N'Keyforge',   N'Україна', N'https://keyforge.example.com'),
    (2, N'Glidex',     N'Польща',  N'https://glidex.example.com'),
    (3, N'SonicCraft', N'Україна', N'https://soniccraft.example.com'),
    (4, N'PadArt',     N'Україна', NULL)
) AS s(Id, Name, Country, Website)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Brands b WHERE b.Id = s.Id);
SET IDENTITY_INSERT dbo.Brands OFF;

SET IDENTITY_INSERT dbo.Categories ON;
INSERT dbo.Categories (Id, Name, Slug, SortOrder, CreatedBy)
SELECT s.Id, s.Name, s.Slug, s.SortOrder, N'seed'
FROM (VALUES
    (1, N'Клавіатури',   N'keyboards',   1),
    (2, N'Миші',         N'mice',        2),
    (3, N'Аудіо',        N'audio',       3),
    (4, N'Килимки',      N'mousepads',   4),
    (5, N'Аксесуари',    N'accessories', 5),
    (6, N'Кастомізація', N'custom',      6),
    (7, N'Новинки',      N'new',         7)
) AS s(Id, Name, Slug, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories c WHERE c.Id = s.Id);
SET IDENTITY_INSERT dbo.Categories OFF;

SET IDENTITY_INSERT dbo.Products ON;
INSERT dbo.Products (Id, BrandId, Sku, Name, Slug, ProductType, Description, BasePrice, Stock, IsCustomizable, CreatedBy)
SELECT s.Id, s.BrandId, s.Sku, s.Name, s.Slug, s.ProductType, s.Description, s.BasePrice, s.Stock, s.IsCustomizable, N'seed'
FROM (VALUES
    (1, 1, N'KB-075-ALU',   N'Клавіатура Aurora 75 (алюміній)', N'aurora-75-alu',     N'Keyboard',   N'Механічна клавіатура 75% в алюмінієвому корпусі, gasket mount', 4500.00, 15, 1),
    (2, 2, N'MS-GLD-01',    N'Миша Glide Pro',                  N'glide-pro',         N'Mouse',      N'Легка ігрова миша 58 г, сенсор 26K DPI',                        2200.00, 30, 1),
    (3, 3, N'HP-STU-02',    N'Навушники Studio Wave',           N'studio-wave',       N'Headphones', N'Закриті студійні навушники зі змінними амбушурами',             3800.00, 12, 1),
    (4, 3, N'SP-DSK-20',    N'Колонки DeskBeat 2.0',            N'deskbeat-20',       N'Speakers',   N'Настільна акустика 2.0, 2×15 Вт',                               3100.00, 10, 0),
    (5, 4, N'MP-XL-900',    N'Килимок Canvas XL 900×400',       N'canvas-xl-900',     N'Mousepad',   N'Тканинний килимок з прошитим краєм, можливий власний принт',     950.00, 50, 1),
    (6, 1, N'KC-PBT-RETRO', N'Набір кейкапів PBT Retro',        N'pbt-retro-keycaps', N'Accessory',  N'Набір 140 кейкапів PBT, профіль Cherry',                        1600.00, 25, 0),
    (7, 1, N'CB-COIL-USBC', N'Кабель Coil USB-C',               N'coil-usb-c',        N'Accessory',  N'Кручений кабель з авіаційним роз''ємом',                          700.00, 40, 1),
    (8, 1, N'KB-060-HS',    N'Клавіатура Pocket 60 Hot-Swap',   N'pocket-60-hs',      N'Keyboard',   N'Компактна 60% клавіатура з hot-swap роз''ємами',                 2900.00, 20, 1)
) AS s(Id, BrandId, Sku, Name, Slug, ProductType, Description, BasePrice, Stock, IsCustomizable)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Products p WHERE p.Id = s.Id);
SET IDENTITY_INSERT dbo.Products OFF;

INSERT dbo.ProductSpecifications (ProductId, WeightGrams, Dimensions, ConnectionType, Material, Color, WarrantyMonths)
SELECT s.ProductId, s.WeightGrams, s.Dimensions, s.ConnectionType, s.Material, s.Color, s.Warranty
FROM (VALUES
    (1, 1650, N'325×140×38 мм', N'Hybrid',    N'Алюміній',            N'Silver', 24),
    (2,   58, N'125×64×39 мм',  N'Wireless',  N'Пластик ABS',         N'Black',  24),
    (3,  290, N'190×170×85 мм', N'Wired',     N'Пластик, метал',      N'Black',  12),
    (4, 2100, N'150×120×190 мм',N'Bluetooth', N'МДФ',                 N'Walnut', 12),
    (5,  540, N'900×400×4 мм',  N'None',      N'Тканина, гума',       N'Black',   6),
    (6,  480, NULL,             N'None',      N'PBT',                 N'Beige',   6),
    (7,   90, N'1.5 м',         N'Wired',     N'Нейлонове обплетення', N'Black',  12),
    (8,  780, N'295×105×35 мм', N'Wired',     N'Пластик',             N'White',  12)
) AS s(ProductId, WeightGrams, Dimensions, ConnectionType, Material, Color, Warranty)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductSpecifications x WHERE x.ProductId = s.ProductId);

INSERT dbo.ProductCategories (ProductId, CategoryId, AddedAt, IsFeatured)
SELECT s.ProductId, s.CategoryId, CAST(s.AddedAt AS DATETIME2), s.IsFeatured
FROM (VALUES
    (1, 1, '2026-06-01', 1), (1, 6, '2026-06-01', 1),
    (8, 1, '2026-09-15', 0), (8, 7, '2026-09-15', 1),
    (2, 2, '2026-06-01', 1), (2, 6, '2026-07-10', 0),
    (3, 3, '2026-06-01', 0), (3, 6, '2026-08-01', 0),
    (4, 3, '2026-06-01', 0),
    (5, 4, '2026-06-01', 1), (5, 6, '2026-06-01', 1),
    (6, 5, '2026-06-20', 0),
    (7, 5, '2026-09-20', 0), (7, 7, '2026-09-20', 0)
) AS s(ProductId, CategoryId, AddedAt, IsFeatured)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductCategories pc
                  WHERE pc.ProductId = s.ProductId AND pc.CategoryId = s.CategoryId);

SET IDENTITY_INSERT dbo.CustomizationOptions ON;
INSERT dbo.CustomizationOptions (Id, ProductId, GroupName, Value, PriceDelta, IsDefault, SortOrder)
SELECT s.Id, s.ProductId, s.GroupName, s.Value, s.PriceDelta, s.IsDefault, s.SortOrder
FROM (VALUES
    ( 1, 1, N'Світчі',        N'Gateron Red',     0.00, 1, 1),
    ( 2, 1, N'Світчі',        N'Holy Panda',    900.00, 0, 2),
    ( 3, 1, N'Кейкапи',       N'PBT Black',       0.00, 1, 1),
    ( 4, 1, N'Кейкапи',       N'Retro Beige',   600.00, 0, 2),
    ( 5, 1, N'Колір корпусу', N'Silver',          0.00, 1, 1),
    ( 6, 1, N'Колір корпусу', N'Midnight Blue', 400.00, 0, 2),
    ( 7, 2, N'Колір корпусу', N'Black',           0.00, 1, 1),
    ( 8, 2, N'Колір корпусу', N'Lilac',         200.00, 0, 2),
    ( 9, 2, N'Покриття',      N'Matte',           0.00, 1, 1),
    (10, 2, N'Покриття',      N'Glossy',        150.00, 0, 2),
    (11, 3, N'Амбушури',      N'Velour',          0.00, 1, 1),
    (12, 3, N'Амбушури',      N'Leather',       450.00, 0, 2),
    (13, 3, N'Гравіювання',   N'Так',           300.00, 0, 1),
    (14, 5, N'Принт',         N'Без принту',      0.00, 1, 1),
    (15, 5, N'Принт',         N'Власний принт', 350.00, 0, 2),
    (16, 8, N'Світчі',        N'Gateron Yellow',  0.00, 1, 1),
    (17, 8, N'Світчі',        N'Kailh Box White',500.00, 0, 2),
    (18, 7, N'Довжина',       N'1.5 м',           0.00, 1, 1),
    (19, 7, N'Довжина',       N'2 м',           100.00, 0, 2)
) AS s(Id, ProductId, GroupName, Value, PriceDelta, IsDefault, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM dbo.CustomizationOptions o WHERE o.Id = s.Id);
SET IDENTITY_INSERT dbo.CustomizationOptions OFF;

SELECT 'Brands' AS TableName, COUNT(*) AS RowsCount FROM dbo.Brands
UNION ALL SELECT 'Categories',            COUNT(*) FROM dbo.Categories
UNION ALL SELECT 'Products',              COUNT(*) FROM dbo.Products
UNION ALL SELECT 'ProductSpecifications', COUNT(*) FROM dbo.ProductSpecifications
UNION ALL SELECT 'ProductCategories',     COUNT(*) FROM dbo.ProductCategories
UNION ALL SELECT 'CustomizationOptions',  COUNT(*) FROM dbo.CustomizationOptions;
GO

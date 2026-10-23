-- Проєкт №2: запити перевірки (після schema.sql і seed.sql).

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE CustomShop_Catalog;
GO
SET NOCOUNT ON;

-- 1. Жодного FK за межі цієї БД
SELECT fk.name AS ForeignKey,
       OBJECT_NAME(fk.parent_object_id)     AS FromTable,
       OBJECT_NAME(fk.referenced_object_id) AS ToTable,
       fk.delete_referential_action_desc    AS OnDelete
FROM sys.foreign_keys fk
ORDER BY FromTable;

-- 2. Індекси
SELECT t.name AS TableName, i.name AS IndexName, i.is_unique, i.has_filter, i.filter_definition
FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
WHERE i.name IS NOT NULL
ORDER BY t.name, i.name;

-- 3. Вітрина: усі клавіатури, від дешевших
SELECT Id, Name, BasePrice, IsCustomizable
FROM dbo.Products
WHERE ProductType = N'Keyboard' AND IsActive = 1 AND IsDeleted = 0
ORDER BY BasePrice;

-- 4. Пошук за назвою + пагінація
SELECT Id, Name, Slug, BasePrice
FROM dbo.Products
WHERE Name LIKE N'Кла%' AND IsActive = 1 AND IsDeleted = 0
ORDER BY Name
OFFSET 0 ROWS FETCH NEXT 3 ROWS ONLY;

-- 5. Категорія «Новинки» (M:N + власне поле AddedAt)
SELECT p.Name, pc.AddedAt, pc.IsFeatured
FROM dbo.ProductCategories pc
JOIN dbo.Categories c ON c.Id = pc.CategoryId
JOIN dbo.Products   p ON p.Id = pc.ProductId
WHERE c.Slug = N'new'
ORDER BY pc.AddedAt DESC;

-- 6. Конфігуратор товару
SELECT GroupName, Value, PriceDelta, IsDefault
FROM dbo.CustomizationOptions
WHERE ProductId = 1 AND IsAvailable = 1
ORDER BY GroupName, SortOrder;

SELECT p.Name,
       p.BasePrice                                     AS MinPrice,
       p.BasePrice + SUM(g.MaxDelta)                   AS MaxPrice
FROM dbo.Products p
JOIN (SELECT ProductId, GroupName, MAX(PriceDelta) AS MaxDelta
      FROM dbo.CustomizationOptions
      GROUP BY ProductId, GroupName) g ON g.ProductId = p.Id
GROUP BY p.Id, p.Name, p.BasePrice;

-- 7. Представлення вітрини
SELECT * FROM dbo.vw_ProductCatalog ORDER BY Id;

-- 8. Обмеження: CHECK і UNIQUE
BEGIN TRY
    UPDATE dbo.Products SET BasePrice = 0 WHERE Id = 1;
END TRY
BEGIN CATCH
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage;   -- 547 (CHECK)
END CATCH;

BEGIN TRY
    UPDATE dbo.CustomizationOptions SET IsDefault = 1 WHERE Id = 2;          -- у групі «Світчі» вже є default
END TRY
BEGIN CATCH
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage;   -- 2601 (UNIQUE)
END CATCH;
GO

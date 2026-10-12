-- Проєкт №2: контекст «Каталог» (SQL Server, EF Core), БД CustomShop_Catalog.
-- Імена PK_*, FK_*, IX_* відповідають конвенціям EF Core, щоб схема з міграцій збіглася з цим скриптом.
-- Каталог — власник даних про товари; інші контексти їх лише копіюють.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF DB_ID(N'CustomShop_Catalog') IS NULL
    CREATE DATABASE CustomShop_Catalog;
GO
USE CustomShop_Catalog;
GO

DROP VIEW  IF EXISTS dbo.vw_ProductCatalog;
DROP TABLE IF EXISTS dbo.CustomizationOptions;
DROP TABLE IF EXISTS dbo.ProductCategories;
DROP TABLE IF EXISTS dbo.ProductSpecifications;
DROP TABLE IF EXISTS dbo.Products;
DROP TABLE IF EXISTS dbo.Categories;
DROP TABLE IF EXISTS dbo.Brands;
GO

CREATE TABLE dbo.Brands (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Brands PRIMARY KEY,
    Name        NVARCHAR(100) NOT NULL,
    Country     NVARCHAR(60)  NULL,
    Website     NVARCHAR(200) NULL
                CONSTRAINT CK_Brands_Website CHECK (Website IS NULL OR Website LIKE N'https://%'),

    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_Brands_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy   NVARCHAR(100) NULL,
    UpdatedAt   DATETIME2     NULL,
    UpdatedBy   NVARCHAR(100) NULL,
    IsDeleted   BIT           NOT NULL CONSTRAINT DF_Brands_IsDeleted DEFAULT 0
);
GO

CREATE TABLE dbo.Categories (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    Name        NVARCHAR(100) NOT NULL,
    Slug        NVARCHAR(100) NOT NULL
                CONSTRAINT CK_Categories_Slug CHECK (Slug NOT LIKE N'%[^a-z0-9-]%'),  -- лише a-z, 0-9, «-»
    SortOrder   INT           NOT NULL CONSTRAINT DF_Categories_SortOrder DEFAULT 0,

    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_Categories_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy   NVARCHAR(100) NULL,
    UpdatedAt   DATETIME2     NULL,
    UpdatedBy   NVARCHAR(100) NULL,
    IsDeleted   BIT           NOT NULL CONSTRAINT DF_Categories_IsDeleted DEFAULT 0
);
GO

-- Products: цей Id копіюють Замовлення (Products.Id) і Відгуки (productId).
-- RowVersion: ціну/склад змінюють паралельно менеджер і обробник подій складу.
CREATE TABLE dbo.Products (
    Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    BrandId        INT           NOT NULL,
    Sku            NVARCHAR(50)  NOT NULL,
    Name           NVARCHAR(200) NOT NULL,
    Slug           NVARCHAR(200) NOT NULL
                   CONSTRAINT CK_Products_Slug CHECK (Slug NOT LIKE N'%[^a-z0-9-]%'),
    ProductType    NVARCHAR(20)  NOT NULL
                   CONSTRAINT CK_Products_ProductType CHECK (ProductType IN
                       (N'Keyboard', N'Mouse', N'Headphones', N'Speakers', N'Mousepad', N'Accessory')),
    Description    NVARCHAR(2000) NULL,
    BasePrice      DECIMAL(18,2) NOT NULL CONSTRAINT CK_Products_BasePrice CHECK (BasePrice > 0),
    Stock          INT           NOT NULL CONSTRAINT DF_Products_Stock DEFAULT 0
                   CONSTRAINT CK_Products_Stock CHECK (Stock >= 0),
    IsCustomizable BIT           NOT NULL CONSTRAINT DF_Products_IsCustomizable DEFAULT 0,
    IsActive       BIT           NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT 1,

    CreatedAt      DATETIME2     NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy      NVARCHAR(100) NULL,
    UpdatedAt      DATETIME2     NULL,
    UpdatedBy      NVARCHAR(100) NULL,
    IsDeleted      BIT           NOT NULL CONSTRAINT DF_Products_IsDeleted DEFAULT 0,
    RowVersion     ROWVERSION    NOT NULL,
    CONSTRAINT FK_Products_Brands_BrandId FOREIGN KEY (BrandId) REFERENCES dbo.Brands(Id) ON DELETE NO ACTION
);
GO

-- ProductSpecifications: 1:1 з Products (PK = FK), каскадне видалення.
CREATE TABLE dbo.ProductSpecifications (
    ProductId      BIGINT        NOT NULL CONSTRAINT PK_ProductSpecifications PRIMARY KEY,
    WeightGrams    INT           NULL CONSTRAINT CK_ProductSpecifications_Weight CHECK (WeightGrams > 0),
    Dimensions     NVARCHAR(50)  NULL,
    ConnectionType NVARCHAR(20)  NOT NULL
                   CONSTRAINT CK_ProductSpecifications_Connection CHECK (ConnectionType IN
                       (N'Wired', N'Wireless', N'Bluetooth', N'Hybrid', N'None')),
    Material       NVARCHAR(100) NULL,
    Color          NVARCHAR(50)  NULL,
    WarrantyMonths INT           NOT NULL CONSTRAINT DF_ProductSpecifications_Warranty DEFAULT 12
                   CONSTRAINT CK_ProductSpecifications_Warranty CHECK (WarrantyMonths BETWEEN 0 AND 60),
    CONSTRAINT FK_ProductSpecifications_Products_ProductId FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id) ON DELETE CASCADE
);
GO

-- ProductCategories: M:N Products <-> Categories з власними полями AddedAt і IsFeatured.
CREATE TABLE dbo.ProductCategories (
    ProductId   BIGINT    NOT NULL,
    CategoryId  INT       NOT NULL,
    AddedAt     DATETIME2 NOT NULL CONSTRAINT DF_ProductCategories_AddedAt DEFAULT SYSUTCDATETIME(),
    IsFeatured  BIT       NOT NULL CONSTRAINT DF_ProductCategories_IsFeatured DEFAULT 0,

    CONSTRAINT PK_ProductCategories PRIMARY KEY (ProductId, CategoryId),
    CONSTRAINT FK_ProductCategories_Products_ProductId FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ProductCategories_Categories_CategoryId FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(Id) ON DELETE CASCADE
);
GO

-- CustomizationOptions: 1:N з Products. Id опції копіюється в Замовлення (OptionId) разом зі знімком.
CREATE TABLE dbo.CustomizationOptions (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomizationOptions PRIMARY KEY,
    ProductId   BIGINT        NOT NULL,
    GroupName   NVARCHAR(100) NOT NULL,
    Value       NVARCHAR(200) NOT NULL,
    PriceDelta  DECIMAL(18,2) NOT NULL CONSTRAINT DF_CustomizationOptions_PriceDelta DEFAULT 0
                CONSTRAINT CK_CustomizationOptions_PriceDelta CHECK (PriceDelta >= 0),
    IsDefault   BIT           NOT NULL CONSTRAINT DF_CustomizationOptions_IsDefault DEFAULT 0,
    IsAvailable BIT           NOT NULL CONSTRAINT DF_CustomizationOptions_IsAvailable DEFAULT 1,
    SortOrder   INT           NOT NULL CONSTRAINT DF_CustomizationOptions_SortOrder DEFAULT 0,
    CONSTRAINT FK_CustomizationOptions_Products_ProductId FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id) ON DELETE CASCADE
);
GO

-- ІНДЕКСИ (над кожним — запит, під який його створено)

-- UNIQUE: назва бренду не повторюється (адмінка, імпорт довідника)
CREATE UNIQUE INDEX IX_Brands_Name ON dbo.Brands (Name);

-- UNIQUE: URL категорії /catalog/{slug}
CREATE UNIQUE INDEX IX_Categories_Slug ON dbo.Categories (Slug);

-- UNIQUE: пошук товару за артикулом (склад, імпорт, синхронізація з Замовленнями)
CREATE UNIQUE INDEX IX_Products_Sku ON dbo.Products (Sku);

-- UNIQUE: сторінка товару /product/{slug}
CREATE UNIQUE INDEX IX_Products_Slug ON dbo.Products (Slug);

-- Запит: вітрина «усі клавіатури, від дешевших до дорожчих».
-- Покриваючий + фільтрований: лише активні, не видалені товари.
CREATE INDEX IX_Products_ProductType_BasePrice
    ON dbo.Products (ProductType, BasePrice)
    INCLUDE (Name, Slug, BrandId, IsCustomizable)
    WHERE IsActive = 1 AND IsDeleted = 0;

-- Запит: пошук за початком назви (Name LIKE N'Клав%') у рядку пошуку
CREATE INDEX IX_Products_Name
    ON dbo.Products (Name)
    INCLUDE (Slug, BasePrice)
    WHERE IsActive = 1 AND IsDeleted = 0;

-- Запит: «усі товари бренду X» + підтримка FK_Products_Brands_BrandId
CREATE INDEX IX_Products_BrandId ON dbo.Products (BrandId);

-- Запит: «товари категорії, нові першими» (сторінка категорії / «Новинки»).
-- PK починається з ProductId, тому для пошуку за CategoryId потрібен свій індекс.
CREATE INDEX IX_ProductCategories_CategoryId_AddedAt
    ON dbo.ProductCategories (CategoryId, AddedAt DESC)
    INCLUDE (IsFeatured);

-- UNIQUE + запит «усі опції товару, згруповані» (конфігуратор на сторінці товару):
-- одне значення не повторюється в групі, індекс починається з ProductId.
CREATE UNIQUE INDEX IX_CustomizationOptions_ProductId_GroupName_Value
    ON dbo.CustomizationOptions (ProductId, GroupName, Value);

-- Фільтрований UNIQUE: у кожній групі не більше ОДНОГО значення за замовчуванням
CREATE UNIQUE INDEX IX_CustomizationOptions_DefaultPerGroup
    ON dbo.CustomizationOptions (ProductId, GroupName)
    WHERE IsDefault = 1;
GO

-- Представлення вітрини: товар + бренд + специфікація + категорії + кількість опцій.
CREATE VIEW dbo.vw_ProductCatalog
AS
SELECT
    p.Id, p.Sku, p.Name, p.Slug, p.ProductType, p.BasePrice, p.Stock, p.IsCustomizable,
    b.Name           AS BrandName,
    s.ConnectionType, s.WeightGrams, s.Material, s.WarrantyMonths,
    (SELECT STRING_AGG(c.Name, N', ') WITHIN GROUP (ORDER BY c.SortOrder)
       FROM dbo.ProductCategories pc
       JOIN dbo.Categories c ON c.Id = pc.CategoryId
      WHERE pc.ProductId = p.Id AND c.IsDeleted = 0)      AS Categories,
    (SELECT COUNT(*) FROM dbo.CustomizationOptions o
      WHERE o.ProductId = p.Id AND o.IsAvailable = 1)     AS OptionsCount
FROM dbo.Products p
JOIN dbo.Brands b                      ON b.Id = p.BrandId
LEFT JOIN dbo.ProductSpecifications s  ON s.ProductId = p.Id
WHERE p.IsDeleted = 0 AND p.IsActive = 1;
GO

PRINT N'Схему CustomShop_Catalog створено.';

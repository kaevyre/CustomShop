-- Проєкт №1: контекст «Замовлення» (SQL Server, ADO.NET & Dapper), БД CustomShop_Orders.
-- Скрипт створює схему з нуля: наявні таблиці видаляються.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF DB_ID(N'CustomShop_Orders') IS NULL
    CREATE DATABASE CustomShop_Orders;
GO
USE CustomShop_Orders;
GO

DROP VIEW  IF EXISTS dbo.vw_OrderSummary;
DROP TABLE IF EXISTS dbo.OrderItemOptions;
DROP TABLE IF EXISTS dbo.OrderItems;
DROP TABLE IF EXISTS dbo.OrderDetails;
DROP TABLE IF EXISTS dbo.Orders;
DROP TABLE IF EXISTS dbo.Products;
DROP TABLE IF EXISTS dbo.Customers;
GO

CREATE TABLE dbo.Customers (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    Email       NVARCHAR(256) NOT NULL
                CONSTRAINT CK_Customers_Email CHECK (Email LIKE N'%_@_%._%'),
    FullName    NVARCHAR(200) NOT NULL
                CONSTRAINT CK_Customers_FullName CHECK (LEN(FullName) >= 2),
    Phone       NVARCHAR(20)  NULL,

    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy   NVARCHAR(100) NULL,
    UpdatedAt   DATETIME2     NULL,
    UpdatedBy   NVARCHAR(100) NULL,
    IsDeleted   BIT           NOT NULL CONSTRAINT DF_Customers_IsDeleted DEFAULT 0,
    RowVersion  ROWVERSION    NOT NULL
);
GO

-- Products — локальна копія довідника Каталогу (Проєкт №2). Id той самий, що в Каталозі, тому не IDENTITY.
-- FOREIGN KEY на іншу БД немає: дані оновлюються подіями з Каталогу.
CREATE TABLE dbo.Products (
    Id          BIGINT        NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    Sku         NVARCHAR(50)  NOT NULL CONSTRAINT UQ_Products_Sku UNIQUE,
    Name        NVARCHAR(200) NOT NULL,
    Price       DECIMAL(18,2) NOT NULL CONSTRAINT CK_Products_Price CHECK (Price > 0),
    Stock       INT           NOT NULL CONSTRAINT DF_Products_Stock DEFAULT 0
                              CONSTRAINT CK_Products_Stock CHECK (Stock >= 0),
    IsActive    BIT           NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT 1,
    SyncedAt    DATETIME2     NOT NULL CONSTRAINT DF_Products_SyncedAt DEFAULT SYSUTCDATETIME(), -- коли застосовано останню подію з Каталогу
    RowVersion  ROWVERSION    NOT NULL  -- склад змінюють паралельно різні замовлення
);
GO

-- Orders: 1:N Customers -> Orders без каскаду (клієнта з історією замовлень видалити не можна).
CREATE TABLE dbo.Orders (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
    CustomerId  BIGINT        NOT NULL,
    Status      NVARCHAR(30)  NOT NULL CONSTRAINT DF_Orders_Status DEFAULT N'Submitted'
                CONSTRAINT CK_Orders_Status CHECK (Status IN (
                    N'Submitted', N'AwaitingValidation', N'StockConfirmed',
                    N'Paid', N'InProduction', N'Shipped', N'Cancelled')),
    OrderDate   DATETIME2     NOT NULL CONSTRAINT DF_Orders_OrderDate DEFAULT SYSUTCDATETIME(),
    PaidAt      DATETIME2     NULL,
    ShippedAt   DATETIME2     NULL,

    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_Orders_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy   NVARCHAR(100) NULL,
    UpdatedAt   DATETIME2     NULL,
    UpdatedBy   NVARCHAR(100) NULL,
    IsDeleted   BIT           NOT NULL CONSTRAINT DF_Orders_IsDeleted DEFAULT 0,
    RowVersion  ROWVERSION    NOT NULL,   -- менеджер і платіжний сервіс можуть змінювати замовлення одночасно

    CONSTRAINT CK_Orders_ShippedAfterPaid
        CHECK (ShippedAt IS NULL OR (PaidAt IS NOT NULL AND ShippedAt >= PaidAt)),
    CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id) ON DELETE NO ACTION
);
GO

-- OrderDetails: 1:1 з Orders (PK = FK), каскадне видалення разом із замовленням.
CREATE TABLE dbo.OrderDetails (
    OrderId         BIGINT        NOT NULL CONSTRAINT PK_OrderDetails PRIMARY KEY,
    RecipientName   NVARCHAR(200) NOT NULL,
    Phone           NVARCHAR(20)  NOT NULL,
    City            NVARCHAR(100) NOT NULL,
    ShippingAddress NVARCHAR(400) NOT NULL,
    DeliveryMethod  NVARCHAR(20)  NOT NULL CONSTRAINT DF_OrderDetails_Delivery DEFAULT N'NovaPoshta'
                    CONSTRAINT CK_OrderDetails_Delivery CHECK (DeliveryMethod IN
                        (N'NovaPoshta', N'UkrPoshta', N'Courier', N'Pickup')),
    EngravingText   NVARCHAR(50)  NULL,   -- текст гравіювання для кастомних товарів
    Comment         NVARCHAR(1000) NULL,
    UpdatedAt       DATETIME2     NULL,
    CONSTRAINT FK_OrderDetails_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id) ON DELETE CASCADE
);
GO

-- OrderItems: проміжна таблиця M:N Orders <-> Products. Сурогатний PK, бо той самий товар
-- може бути в замовленні двічі з різними налаштуваннями.
-- ProductName, UnitPrice — ЗНІМОК на момент оформлення (не оновлюється).
CREATE TABLE dbo.OrderItems (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderItems PRIMARY KEY,
    OrderId     BIGINT        NOT NULL,
    ProductId   BIGINT        NOT NULL,
    ProductName NVARCHAR(200) NOT NULL,            -- знімок
    UnitPrice   DECIMAL(18,2) NOT NULL CONSTRAINT CK_OrderItems_UnitPrice CHECK (UnitPrice > 0), -- знімок базової ціни
    Units       INT           NOT NULL CONSTRAINT CK_OrderItems_Units CHECK (Units BETWEEN 1 AND 50),
    Discount    DECIMAL(18,2) NOT NULL CONSTRAINT DF_OrderItems_Discount DEFAULT 0
                CONSTRAINT CK_OrderItems_DiscountPositive CHECK (Discount >= 0),
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_OrderItems_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT CK_OrderItems_DiscountLessThanPrice CHECK (Discount <= UnitPrice),
    CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
    CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id) ON DELETE NO ACTION
);
GO

-- OrderItemOptions: 1:N з OrderItems. OptionId — Id опції в Каталозі, БЕЗ FK.
-- OptionGroup, OptionValue, PriceDelta — ЗНІМОК. UNIQUE: одне значення кожної групи на позицію.
CREATE TABLE dbo.OrderItemOptions (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderItemOptions PRIMARY KEY,
    OrderItemId BIGINT        NOT NULL,
    OptionId    BIGINT        NOT NULL,            -- посилання на Каталог без FK
    OptionGroup NVARCHAR(100) NOT NULL,            -- знімок, напр. «Світчі»
    OptionValue NVARCHAR(200) NOT NULL,            -- знімок, напр. «Holy Panda»
    PriceDelta  DECIMAL(18,2) NOT NULL CONSTRAINT DF_OrderItemOptions_PriceDelta DEFAULT 0
                CONSTRAINT CK_OrderItemOptions_PriceDelta CHECK (PriceDelta >= 0),

    CONSTRAINT UQ_OrderItemOptions_Item_Group UNIQUE (OrderItemId, OptionGroup),
    CONSTRAINT FK_OrderItemOptions_OrderItems FOREIGN KEY (OrderItemId) REFERENCES dbo.OrderItems(Id) ON DELETE CASCADE
);
GO

-- ІНДЕКСИ (над кожним — запит, під який його створено)

-- Запит: пошук/вхід клієнта за email («чи є вже такий покупець?»).
-- Фільтрований UNIQUE: email унікальний лише серед НЕ видалених клієнтів,
-- тож після м'якого видалення той самий email можна зареєструвати знову.
CREATE UNIQUE INDEX UX_Customers_Email
    ON dbo.Customers (Email)
    WHERE IsDeleted = 0;

-- Запит: пошук клієнта менеджером за початком ПІБ (FullName LIKE N'Кова%').
CREATE INDEX IX_Customers_FullName
    ON dbo.Customers (FullName)
    INCLUDE (Email, Phone)
    WHERE IsDeleted = 0;

-- Запит: «замовлення клієнта з певним статусом, новіші першими»
-- (usp_GetCustomerOrders, особистий кабінет). Покриваючий + фільтрований.
CREATE INDEX IX_Orders_CustomerId_Status
    ON dbo.Orders (CustomerId, Status, OrderDate DESC)
    WHERE IsDeleted = 0;

-- Запит: черга менеджера/цеху — «усі замовлення у статусі X за датою»
-- (наприклад, що зараз InProduction, найстаріші першими).
CREATE INDEX IX_Orders_Status_OrderDate
    ON dbo.Orders (Status, OrderDate)
    INCLUDE (CustomerId)
    WHERE IsDeleted = 0;

-- Запит: завантажити всі позиції замовлення (агрегат Order + OrderItems).
-- Покриваючий: читання позицій не звертається до кластерного індексу.
CREATE INDEX IX_OrderItems_OrderId
    ON dbo.OrderItems (OrderId)
    INCLUDE (ProductId, ProductName, UnitPrice, Units, Discount);

-- Запит: «у яких замовленнях є товар X» (обробка події ProductDeactivated,
-- розрахунок резерву складу в usp_ChangeOrderStatus).
CREATE INDEX IX_OrderItems_ProductId
    ON dbo.OrderItems (ProductId)
    INCLUDE (OrderId, Units);

GO

-- Представлення для читання: замовлення + клієнт + доставка + сума
-- (сума = (ціна + доплати за опції − знижка) × кількість).
CREATE VIEW dbo.vw_OrderSummary
AS
SELECT
    o.Id                    AS OrderId,
    o.CustomerId,
    c.FullName              AS CustomerName,
    c.Email                 AS CustomerEmail,
    o.Status,
    o.OrderDate,
    o.PaidAt,
    o.ShippedAt,
    d.City,
    d.DeliveryMethod,
    COUNT(i.Id)             AS ItemsCount,
    COALESCE(SUM(i.Units), 0) AS UnitsTotal,
    CAST(COALESCE(SUM((i.UnitPrice + COALESCE(opt.OptionsTotal, 0) - i.Discount) * i.Units), 0)
         AS DECIMAL(18,2)) AS TotalAmount
FROM dbo.Orders o
JOIN dbo.Customers c        ON c.Id = o.CustomerId
LEFT JOIN dbo.OrderDetails d ON d.OrderId = o.Id
LEFT JOIN dbo.OrderItems i   ON i.OrderId = o.Id
LEFT JOIN (
    SELECT OrderItemId, SUM(PriceDelta) AS OptionsTotal
    FROM dbo.OrderItemOptions
    GROUP BY OrderItemId
) opt ON opt.OrderItemId = i.Id
WHERE o.IsDeleted = 0
GROUP BY o.Id, o.CustomerId, c.FullName, c.Email, o.Status, o.OrderDate,
         o.PaidAt, o.ShippedAt, d.City, d.DeliveryMethod;
GO

PRINT N'Схему CustomShop_Orders створено.';

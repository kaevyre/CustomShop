-- Проєкт №1: тестові дані. Ідемпотентний: кожна вставка перевіряє наявність запису (WHERE NOT EXISTS).
-- Id товарів і опцій збігаються з БД Каталогу.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE CustomShop_Orders;
GO
SET NOCOUNT ON;

INSERT dbo.Products (Id, Sku, Name, Price, Stock)
SELECT s.Id, s.Sku, s.Name, s.Price, s.Stock
FROM (VALUES
    (1, N'KB-075-ALU',   N'Клавіатура Aurora 75 (алюміній)', 4500.00, 15),
    (2, N'MS-GLD-01',    N'Миша Glide Pro',                  2200.00, 30),
    (3, N'HP-STU-02',    N'Навушники Studio Wave',           3800.00, 12),
    (4, N'SP-DSK-20',    N'Колонки DeskBeat 2.0',            3100.00, 10),
    (5, N'MP-XL-900',    N'Килимок Canvas XL 900×400',        950.00, 50),
    (6, N'KC-PBT-RETRO', N'Набір кейкапів PBT Retro',        1600.00, 25),
    (7, N'CB-COIL-USBC', N'Кабель Coil USB-C',                700.00, 40),
    (8, N'KB-060-HS',    N'Клавіатура Pocket 60 Hot-Swap',   2900.00, 20)
) AS s(Id, Sku, Name, Price, Stock)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Products p WHERE p.Id = s.Id);

INSERT dbo.Customers (Email, FullName, Phone, CreatedBy)
SELECT s.Email, s.FullName, s.Phone, N'seed'
FROM (VALUES
    (N'olena.koval@example.com',    N'Олена Коваль',   N'+380671112233'),
    (N'andrii.melnyk@example.com',  N'Андрій Мельник', N'+380502223344'),
    (N'maria.shevchuk@example.com', N'Марія Шевчук',   N'+380933334455'),
    (N'taras.boiko@example.com',    N'Тарас Бойко',    N'+380974445566')
) AS s(Email, FullName, Phone)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers c WHERE c.Email = s.Email);

SET IDENTITY_INSERT dbo.Orders ON;

INSERT dbo.Orders (Id, CustomerId, Status, OrderDate, PaidAt, ShippedAt, CreatedBy)
SELECT s.Id, c.Id, s.Status,
       CAST(s.OrderDate AS DATETIME2), CAST(s.PaidAt AS DATETIME2), CAST(s.ShippedAt AS DATETIME2),
       N'seed'
FROM (VALUES
    (1, N'olena.koval@example.com',    N'Shipped',      '2026-09-02T10:15:00', '2026-09-02T12:00:00', '2026-09-09T09:30:00'),
    (2, N'andrii.melnyk@example.com',  N'InProduction', '2026-09-20T18:40:00', '2026-09-21T08:05:00', NULL),
    (3, N'maria.shevchuk@example.com', N'Paid',         '2026-10-05T14:00:00', '2026-10-05T14:20:00', NULL),
    (4, N'olena.koval@example.com',    N'Submitted',    '2026-10-10T20:10:00', NULL,                  NULL),
    (5, N'taras.boiko@example.com',    N'Cancelled',    '2026-09-28T11:00:00', NULL,                  NULL)
) AS s(Id, Email, Status, OrderDate, PaidAt, ShippedAt)
JOIN dbo.Customers c ON c.Email = s.Email
WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders o WHERE o.Id = s.Id);

SET IDENTITY_INSERT dbo.Orders OFF;

INSERT dbo.OrderDetails (OrderId, RecipientName, Phone, City, ShippingAddress, DeliveryMethod, EngravingText, Comment)
SELECT s.OrderId, s.RecipientName, s.Phone, s.City, s.Address, s.Delivery, s.Engraving, s.Comment
FROM (VALUES
    (1, N'Олена Коваль',   N'+380671112233', N'Львів',  N'Відділення НП №12', N'NovaPoshta', NULL,        N'Принт для килимка — файл надіслано на пошту'),
    (2, N'Андрій Мельник', N'+380502223344', N'Київ',   N'вул. Січових Стрільців, 21, кв. 5', N'Courier', NULL, NULL),
    (3, N'Марія Шевчук',   N'+380933334455', N'Одеса',  N'Відділення НП №3',  N'NovaPoshta', N'M.S.',      NULL),
    (4, N'Олена Коваль',   N'+380671112233', N'Львів',  N'Відділення НП №12', N'NovaPoshta', NULL,        NULL),
    (5, N'Тарас Бойко',    N'+380974445566', N'Харків', N'Самовивіз зі складу', N'Pickup',   NULL,        N'Передумав')
) AS s(OrderId, RecipientName, Phone, City, Address, Delivery, Engraving, Comment)
WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderDetails d WHERE d.OrderId = s.OrderId)
  AND EXISTS (SELECT 1 FROM dbo.Orders o WHERE o.Id = s.OrderId);

-- UnitPrice у позиції 1 (4200) свідомо відрізняється від поточної ціни в Products (4500): знімок не змінюється.
SET IDENTITY_INSERT dbo.OrderItems ON;

INSERT dbo.OrderItems (Id, OrderId, ProductId, ProductName, UnitPrice, Units, Discount)
SELECT s.Id, s.OrderId, s.ProductId, s.ProductName, s.UnitPrice, s.Units, s.Discount
FROM (VALUES
    (1, 1, 1, N'Клавіатура Aurora 75 (алюміній)', 4200.00, 1, 200.00),
    (2, 1, 5, N'Килимок Canvas XL 900×400',        950.00, 1,   0.00),
    (3, 2, 2, N'Миша Glide Pro',                  2200.00, 1,   0.00),
    (4, 3, 3, N'Навушники Studio Wave',           3800.00, 1,   0.00),
    (5, 4, 4, N'Колонки DeskBeat 2.0',            3100.00, 1,   0.00),
    (6, 4, 7, N'Кабель Coil USB-C',                700.00, 2,  50.00),
    (7, 5, 6, N'Набір кейкапів PBT Retro',        1600.00, 1,   0.00)
) AS s(Id, OrderId, ProductId, ProductName, UnitPrice, Units, Discount)
WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems i WHERE i.Id = s.Id)
  AND EXISTS (SELECT 1 FROM dbo.Orders o WHERE o.Id = s.OrderId);

SET IDENTITY_INSERT dbo.OrderItems OFF;

SET IDENTITY_INSERT dbo.OrderItemOptions ON;

INSERT dbo.OrderItemOptions (Id, OrderItemId, OptionId, OptionGroup, OptionValue, PriceDelta)
SELECT s.Id, s.OrderItemId, s.OptionId, s.OptionGroup, s.OptionValue, s.PriceDelta
FROM (VALUES
    (1, 1,  2, N'Світчі',        N'Holy Panda',     900.00),
    (2, 1,  4, N'Кейкапи',       N'Retro Beige',    600.00),
    (3, 1,  5, N'Колір корпусу', N'Silver',           0.00),
    (4, 2, 15, N'Принт',         N'Власний принт',  350.00),
    (5, 3,  8, N'Колір корпусу', N'Lilac',          200.00),
    (6, 3,  9, N'Покриття',      N'Matte',            0.00),
    (7, 4, 12, N'Амбушури',      N'Leather',        450.00),
    (8, 4, 13, N'Гравіювання',   N'Так',            300.00),
    (9, 6, 19, N'Довжина',       N'2 м',            100.00)
) AS s(Id, OrderItemId, OptionId, OptionGroup, OptionValue, PriceDelta)
WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItemOptions x WHERE x.Id = s.Id)
  AND EXISTS (SELECT 1 FROM dbo.OrderItems i WHERE i.Id = s.OrderItemId);

SET IDENTITY_INSERT dbo.OrderItemOptions OFF;

SELECT 'Customers' AS TableName, COUNT(*) AS RowsCount FROM dbo.Customers
UNION ALL SELECT 'Products',         COUNT(*) FROM dbo.Products
UNION ALL SELECT 'Orders',           COUNT(*) FROM dbo.Orders
UNION ALL SELECT 'OrderDetails',     COUNT(*) FROM dbo.OrderDetails
UNION ALL SELECT 'OrderItems',       COUNT(*) FROM dbo.OrderItems
UNION ALL SELECT 'OrderItemOptions', COUNT(*) FROM dbo.OrderItemOptions;
GO

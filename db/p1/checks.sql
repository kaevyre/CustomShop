-- Проєкт №1: запити перевірки. Запускати після schema.sql, procedures.sql, seed.sql.
-- Блок 5 створює нові тестові записи; seed-дані не дублює.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE CustomShop_Orders;
GO
SET NOCOUNT ON;

-- 1. Жодного FK за межі цієї БД
SELECT fk.name                               AS ForeignKey,
       OBJECT_NAME(fk.parent_object_id)      AS FromTable,
       OBJECT_NAME(fk.referenced_object_id)  AS ToTable,
       fk.delete_referential_action_desc     AS OnDelete
FROM sys.foreign_keys AS fk
ORDER BY FromTable;

-- 2. Індекси та обмеження
SELECT t.name AS TableName, i.name AS IndexName, i.type_desc, i.is_unique, i.has_filter, i.filter_definition
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
WHERE i.name IS NOT NULL
ORDER BY t.name, i.name;

SELECT OBJECT_NAME(parent_object_id) AS TableName, name AS CheckName, definition
FROM sys.check_constraints
ORDER BY TableName;

-- 3. READ: пошук і пагінація
SELECT Id, FullName, Email
FROM dbo.Customers
WHERE IsDeleted = 0 AND FullName LIKE N'Кова%';

EXEC dbo.usp_GetCustomerOrders @CustomerId = 1, @PageNumber = 1, @PageSize = 2;

EXEC dbo.usp_GetCustomerOrders @CustomerId = 1, @Status = N'Submitted';

-- черга цеху: що зараз виготовляється (IX_Orders_Status_OrderDate)
SELECT Id, CustomerId, OrderDate
FROM dbo.Orders
WHERE Status = N'InProduction' AND IsDeleted = 0
ORDER BY OrderDate;

SELECT i.Id AS ItemId, i.ProductName, i.UnitPrice, i.Units, i.Discount,
       o.OptionGroup, o.OptionValue, o.PriceDelta
FROM dbo.OrderItems i
LEFT JOIN dbo.OrderItemOptions o ON o.OrderItemId = i.Id
WHERE i.OrderId = 1
ORDER BY i.Id, o.OptionGroup;

-- знімок vs поточна ціна: ціна в замовленні не змінилась після подорожчання
SELECT i.ProductName, i.UnitPrice AS PriceInOrder, p.Price AS CurrentPrice
FROM dbo.OrderItems i
JOIN dbo.Products p ON p.Id = i.ProductId
WHERE i.OrderId = 1;
GO

-- 4. CREATE / UPDATE / DELETE клієнта
DECLARE @CustomerId BIGINT, @Rv BINARY(8);

DECLARE @Email NVARCHAR(256) = CONCAT(N'test.', FORMAT(SYSUTCDATETIME(), 'yyyyMMddHHmmssfff'), N'@example.com');
EXEC dbo.usp_UpsertCustomer @Id = @CustomerId OUTPUT, @Email = @Email,
     @FullName = N'Тестовий Покупець', @Phone = N'+380990000000', @User = N'checks';

SELECT @Rv = RowVersion FROM dbo.Customers WHERE Id = @CustomerId;
EXEC dbo.usp_UpsertCustomer @Id = @CustomerId OUTPUT, @Email = @Email,
     @FullName = N'Тестовий Покупець (оновлено)', @ExpectedRowVersion = @Rv, @User = N'checks';

-- UPDATE зі ЗАСТАРІЛИМ RowVersion -> керована помилка 50003
BEGIN TRY
    EXEC dbo.usp_UpsertCustomer @Id = @CustomerId OUTPUT, @Email = @Email,
         @FullName = N'Чужі зміни', @ExpectedRowVersion = @Rv, @User = N'checks';
END TRY
BEGIN CATCH
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage;  -- 50003
END CATCH;

EXEC dbo.usp_DeleteCustomer @Id = @CustomerId, @User = N'checks';
SELECT Id, FullName, IsDeleted FROM dbo.Customers WHERE Id = @CustomerId;
GO

-- 5. Транзакційний сценарій: життєвий цикл замовлення, недопустимі переходи
DECLARE @OrderId BIGINT, @ItemId BIGINT;

EXEC dbo.usp_CreateOrder
     @CustomerId = 2, @RecipientName = N'Андрій Мельник', @Phone = N'+380502223344',
     @City = N'Київ', @ShippingAddress = N'Відділення НП №1', @User = N'checks',
     @OrderId = @OrderId OUTPUT;

EXEC dbo.usp_AddOrderItem
     @OrderId = @OrderId, @ProductId = 8, @Units = 1, @User = N'checks',
     @OptionsJson = N'[{"optionId":17,"group":"Світчі","value":"Kailh Box White","priceDelta":500}]',
     @OrderItemId = @ItemId OUTPUT;

SELECT Stock AS StockBefore FROM dbo.Products WHERE Id = 8;

EXEC dbo.usp_ChangeOrderStatus @OrderId = @OrderId, @NewStatus = N'AwaitingValidation', @User = N'checks';
EXEC dbo.usp_ChangeOrderStatus @OrderId = @OrderId, @NewStatus = N'StockConfirmed',     @User = N'checks';

SELECT Stock AS StockAfterReserve FROM dbo.Products WHERE Id = 8;   -- зменшився на 1

-- НЕДОПУСТИМИЙ перехід: відвантажити неоплачене замовлення -> 50002, статус не змінюється
BEGIN TRY
    EXEC dbo.usp_ChangeOrderStatus @OrderId = @OrderId, @NewStatus = N'Shipped', @User = N'checks';
END TRY
BEGIN CATCH
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage;   -- 50002
END CATCH;

SELECT Id, Status FROM dbo.Orders WHERE Id = @OrderId;   -- досі StockConfirmed
SELECT @@TRANCOUNT AS OpenTransactions;                   -- 0: процедура сама відкотила транзакцію

-- скасування до оплати дозволене, резерв повертається на склад
EXEC dbo.usp_ChangeOrderStatus @OrderId = @OrderId, @NewStatus = N'Cancelled', @User = N'checks';
SELECT Stock AS StockAfterCancel FROM dbo.Products WHERE Id = 8;

-- скасувати вже ОПЛАЧЕНЕ замовлення №3 не можна -> 50002
BEGIN TRY
    EXEC dbo.usp_ChangeOrderStatus @OrderId = 3, @NewStatus = N'Cancelled', @User = N'checks';
END TRY
BEGIN CATCH
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage;   -- 50002
END CATCH;

-- додати позицію до НЕ нового замовлення -> 50007
BEGIN TRY
    EXEC dbo.usp_AddOrderItem @OrderId = 2, @ProductId = 7, @Units = 1;
END TRY
BEGIN CATCH
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage;   -- 50007
END CATCH;

-- клієнта з активним замовленням видалити не можна -> 50008
BEGIN TRY
    EXEC dbo.usp_DeleteCustomer @Id = 1;
END TRY
BEGIN CATCH
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage;   -- 50008
END CATCH;
GO

-- 6. Обмеження на рівні БД
BEGIN TRY
    UPDATE dbo.Products SET Price = -10 WHERE Id = 1;     -- CK_Products_Price
END TRY
BEGIN CATCH
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage;  -- 547
END CATCH;

BEGIN TRY
    DELETE FROM dbo.Customers WHERE Id = 1;               -- FK_Orders_Customers (без каскаду)
END TRY
BEGIN CATCH
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage;  -- 547
END CATCH;
GO

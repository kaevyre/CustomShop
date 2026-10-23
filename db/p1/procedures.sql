/* Проєкт №1 — збережувані процедури. БД: CustomShop_Orders.
   Коди керованих помилок (THROW):
     50001 замовлення не знайдено         50007 замовлення не можна редагувати
     50002 недопустимий перехід статусу   50008 у клієнта є активні замовлення
     50003 конфлікт конкурентності        50009 некоректний JSON опцій
     50004 замовлення без позицій         50010 клієнта не знайдено
     50005 недостатньо товару на складі   50011 email уже зайнятий
     50006 товар не знайдено / неактивний 50012 некоректні параметри пагінації */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE CustomShop_Orders;
GO

-- 1. usp_UpsertCustomer: @Id = NULL — створення, інакше оновлення з перевіркою @ExpectedRowVersion (50003).
CREATE OR ALTER PROCEDURE dbo.usp_UpsertCustomer
    @Id                 BIGINT        = NULL OUTPUT,
    @Email              NVARCHAR(256),
    @FullName           NVARCHAR(200),
    @Phone              NVARCHAR(20)  = NULL,
    @ExpectedRowVersion BINARY(8)     = NULL,
    @User               NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK)
                   WHERE Email = @Email AND IsDeleted = 0 AND (@Id IS NULL OR Id <> @Id))
            THROW 50011, N'Customer with this email already exists', 1;

        IF @Id IS NULL
        BEGIN
            INSERT dbo.Customers (Email, FullName, Phone, CreatedBy)
            VALUES (@Email, @FullName, @Phone, @User);

            SET @Id = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            DECLARE @CurrentRv BINARY(8);

            SELECT @CurrentRv = RowVersion
            FROM dbo.Customers WITH (UPDLOCK)
            WHERE Id = @Id AND IsDeleted = 0;

            IF @CurrentRv IS NULL
                THROW 50010, N'Customer not found', 1;

            IF @ExpectedRowVersion IS NOT NULL AND @ExpectedRowVersion <> @CurrentRv
                THROW 50003, N'Concurrency conflict: customer was modified by another user', 1;

            UPDATE dbo.Customers
            SET Email     = @Email,
                FullName  = @FullName,
                Phone     = @Phone,
                UpdatedAt = SYSUTCDATETIME(),
                UpdatedBy = @User
            WHERE Id = @Id;
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT Id, Email, FullName, Phone, CreatedAt, UpdatedAt, RowVersion
    FROM dbo.Customers
    WHERE Id = @Id;
END
GO

-- 2. usp_DeleteCustomer: м'яке видалення; відмова, якщо є незавершені замовлення (50008).
CREATE OR ALTER PROCEDURE dbo.usp_DeleteCustomer
    @Id   BIGINT,
    @User NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.Customers WITH (UPDLOCK)
                       WHERE Id = @Id AND IsDeleted = 0)
            THROW 50010, N'Customer not found', 1;

        IF EXISTS (SELECT 1 FROM dbo.Orders
                   WHERE CustomerId = @Id AND IsDeleted = 0
                     AND Status NOT IN (N'Shipped', N'Cancelled'))
            THROW 50008, N'Customer has active orders and cannot be deleted', 1;

        UPDATE dbo.Customers
        SET IsDeleted = 1,
            UpdatedAt = SYSUTCDATETIME(),
            UpdatedBy = @User
        WHERE Id = @Id;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END
GO

-- 3. usp_GetCustomerOrders: замовлення клієнта з фільтром за статусом і пагінацією (IX_Orders_CustomerId_Status).
CREATE OR ALTER PROCEDURE dbo.usp_GetCustomerOrders
    @CustomerId BIGINT,
    @Status     NVARCHAR(30) = NULL,
    @PageNumber INT          = 1,
    @PageSize   INT          = 10
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber < 1 OR @PageSize NOT BETWEEN 1 AND 100
        THROW 50012, N'PageNumber must be >= 1 and PageSize between 1 and 100', 1;

    SELECT
        OrderId, Status, OrderDate, PaidAt, ShippedAt, City, DeliveryMethod,
        ItemsCount, UnitsTotal, TotalAmount,
        COUNT(*) OVER () AS TotalCount
    FROM dbo.vw_OrderSummary
    WHERE CustomerId = @CustomerId
      AND (@Status IS NULL OR Status = @Status)
    ORDER BY OrderDate DESC, OrderId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);   -- окремий план для варіантів «з фільтром статусу» і «без нього»
END
GO

-- 4. usp_CreateOrder: замовлення + деталі доставки (1:1) в одній транзакції.
CREATE OR ALTER PROCEDURE dbo.usp_CreateOrder
    @CustomerId      BIGINT,
    @RecipientName   NVARCHAR(200),
    @Phone           NVARCHAR(20),
    @City            NVARCHAR(100),
    @ShippingAddress NVARCHAR(400),
    @DeliveryMethod  NVARCHAR(20)   = N'NovaPoshta',
    @EngravingText   NVARCHAR(50)   = NULL,
    @Comment         NVARCHAR(1000) = NULL,
    @User            NVARCHAR(100)  = NULL,
    @OrderId         BIGINT         = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Id = @CustomerId AND IsDeleted = 0)
            THROW 50010, N'Customer not found', 1;

        INSERT dbo.Orders (CustomerId, CreatedBy)
        VALUES (@CustomerId, @User);

        SET @OrderId = SCOPE_IDENTITY();

        INSERT dbo.OrderDetails
            (OrderId, RecipientName, Phone, City, ShippingAddress, DeliveryMethod, EngravingText, Comment)
        VALUES
            (@OrderId, @RecipientName, @Phone, @City, @ShippingAddress, @DeliveryMethod, @EngravingText, @Comment);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT @OrderId AS OrderId;
END
GO

-- 5. usp_AddOrderItem: позиція зі ЗНІМКОМ назви й ціни з Products; опції — JSON-масив,
--    напр. [{"optionId":2,"group":"Світчі","value":"Holy Panda","priceDelta":900}].
CREATE OR ALTER PROCEDURE dbo.usp_AddOrderItem
    @OrderId     BIGINT,
    @ProductId   BIGINT,
    @Units       INT           = 1,
    @Discount    DECIMAL(18,2) = 0,
    @OptionsJson NVARCHAR(MAX) = NULL,
    @User        NVARCHAR(100) = NULL,
    @OrderItemId BIGINT        = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @OptionsJson IS NOT NULL AND ISJSON(@OptionsJson) = 0
        THROW 50009, N'OptionsJson is not valid JSON', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(30) =
            (SELECT Status FROM dbo.Orders WITH (UPDLOCK) WHERE Id = @OrderId AND IsDeleted = 0);

        IF @Status IS NULL
            THROW 50001, N'Order not found', 1;

        IF @Status <> N'Submitted'
            THROW 50007, N'Items can be added only to an order in Submitted status', 1;

        DECLARE @Name NVARCHAR(200), @Price DECIMAL(18,2), @Stock INT;

        SELECT @Name = Name, @Price = Price, @Stock = Stock
        FROM dbo.Products
        WHERE Id = @ProductId AND IsActive = 1;

        IF @Name IS NULL
            THROW 50006, N'Product not found or inactive', 1;

        IF @Stock < @Units
            THROW 50005, N'Insufficient stock for this product', 1;

        INSERT dbo.OrderItems (OrderId, ProductId, ProductName, UnitPrice, Units, Discount)
        VALUES (@OrderId, @ProductId, @Name, @Price, @Units, @Discount);

        SET @OrderItemId = SCOPE_IDENTITY();

        IF @OptionsJson IS NOT NULL
            INSERT dbo.OrderItemOptions (OrderItemId, OptionId, OptionGroup, OptionValue, PriceDelta)
            SELECT @OrderItemId, j.optionId, j.optionGroup, j.optionValue, ISNULL(j.priceDelta, 0)
            FROM OPENJSON(@OptionsJson)
                 WITH (
                     optionId    BIGINT        '$.optionId',
                     optionGroup NVARCHAR(100) '$.group',
                     optionValue NVARCHAR(200) '$.value',
                     priceDelta  DECIMAL(18,2) '$.priceDelta'
                 ) AS j;

        UPDATE dbo.Orders
        SET UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @User
        WHERE Id = @OrderId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT @OrderItemId AS OrderItemId;
END
GO

/* 6. usp_ChangeOrderStatus — транзакційна зміна статусу.
   Допустимі переходи:
     Submitted -> AwaitingValidation | Cancelled
     AwaitingValidation -> StockConfirmed | Cancelled
     StockConfirmed -> Paid | Cancelled
     Paid -> InProduction -> Shipped
   Інший перехід — помилка 50002. -> StockConfirmed резервує склад,
   StockConfirmed -> Cancelled повертає його. */
CREATE OR ALTER PROCEDURE dbo.usp_ChangeOrderStatus
    @OrderId            BIGINT,
    @NewStatus          NVARCHAR(30),
    @ExpectedRowVersion BINARY(8)     = NULL,
    @User               NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @msg NVARCHAR(400);
    DECLARE @Status NVARCHAR(30), @CurrentRv BINARY(8);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @Status = Status, @CurrentRv = RowVersion
        FROM dbo.Orders WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @OrderId AND IsDeleted = 0;

        IF @Status IS NULL
            THROW 50001, N'Order not found', 1;

        IF @ExpectedRowVersion IS NOT NULL AND @ExpectedRowVersion <> @CurrentRv
            THROW 50003, N'Concurrency conflict: order was modified by another user', 1;

        IF NOT EXISTS (
            SELECT 1
            FROM (VALUES
                    (N'Submitted',          N'AwaitingValidation'),
                    (N'Submitted',          N'Cancelled'),
                    (N'AwaitingValidation', N'StockConfirmed'),
                    (N'AwaitingValidation', N'Cancelled'),
                    (N'StockConfirmed',     N'Paid'),
                    (N'StockConfirmed',     N'Cancelled'),
                    (N'Paid',               N'InProduction'),
                    (N'InProduction',       N'Shipped')
                 ) AS t(FromStatus, ToStatus)
            WHERE t.FromStatus = @Status AND t.ToStatus = @NewStatus)
        BEGIN
            SET @msg = CONCAT(N'Invalid status transition: ', @Status, N' -> ', @NewStatus);
            THROW 50002, @msg, 1;
        END

        IF @NewStatus = N'AwaitingValidation'
           AND NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderId = @OrderId)
            THROW 50004, N'Order has no items', 1;

        IF @NewStatus = N'StockConfirmed'
        BEGIN
            DECLARE @ProductsNeeded INT =
                (SELECT COUNT(DISTINCT ProductId) FROM dbo.OrderItems WHERE OrderId = @OrderId);

            UPDATE p
            SET p.Stock = p.Stock - n.Need
            FROM dbo.Products AS p
            JOIN (SELECT ProductId, SUM(Units) AS Need
                  FROM dbo.OrderItems
                  WHERE OrderId = @OrderId
                  GROUP BY ProductId) AS n
              ON n.ProductId = p.Id
            WHERE p.Stock >= n.Need AND p.IsActive = 1;

            IF @@ROWCOUNT <> @ProductsNeeded
                THROW 50005, N'Insufficient stock for one or more products', 1;
        END

        IF @NewStatus = N'Cancelled' AND @Status = N'StockConfirmed'
        BEGIN
            UPDATE p
            SET p.Stock = p.Stock + n.Need
            FROM dbo.Products AS p
            JOIN (SELECT ProductId, SUM(Units) AS Need
                  FROM dbo.OrderItems
                  WHERE OrderId = @OrderId
                  GROUP BY ProductId) AS n
              ON n.ProductId = p.Id;
        END

        UPDATE dbo.Orders
        SET Status    = @NewStatus,
            PaidAt    = CASE WHEN @NewStatus = N'Paid'    THEN SYSUTCDATETIME() ELSE PaidAt    END,
            ShippedAt = CASE WHEN @NewStatus = N'Shipped' THEN SYSUTCDATETIME() ELSE ShippedAt END,
            UpdatedAt = SYSUTCDATETIME(),
            UpdatedBy = @User
        WHERE Id = @OrderId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT Id AS OrderId, Status, PaidAt, ShippedAt, RowVersion
    FROM dbo.Orders
    WHERE Id = @OrderId;
END
GO

PRINT N'Процедури CustomShop_Orders створено.';

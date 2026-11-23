-- Лаб. 08: ідемпотентність споживача подій. Застосовується до БД CustomShop_Orders, створених раніше
-- (нові БД отримують таблицю зі схеми db/p1/schema.sql). Скрипт ідемпотентний; те саме виконує Orders.Api на старті
-- consumer-а (ProcessedEventRepository.EnsureSchemaAsync), тож ручне застосування не обов'язкове.
IF OBJECT_ID(N'dbo.ProcessedEvents', N'U') IS NULL
    CREATE TABLE dbo.ProcessedEvents (
        EventId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProcessedEvents PRIMARY KEY,   -- EventId події з Каталогу
        ProcessedAt DATETIME2        NOT NULL CONSTRAINT DF_ProcessedEvents_ProcessedAt DEFAULT SYSUTCDATETIME()
    );
GO
-- TryAddAsync: INSERT; порушення PRIMARY KEY (помилка 2627) означає «подію вже оброблено».
-- Запис EventId і зміна Products виконуються в ОДНІЙ транзакції (див. Orders.Api/IntegrationEvents).

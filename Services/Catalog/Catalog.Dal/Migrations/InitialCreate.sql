IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    CREATE TABLE [Brands] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Country] nvarchar(60) NULL,
        [Website] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Brands] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Brands_Website] CHECK ([Website] IS NULL OR [Website] LIKE N'https://%')
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Slug] nvarchar(100) NOT NULL,
        [SortOrder] int NOT NULL DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Categories_Slug] CHECK ([Slug] NOT LIKE N'%[^a-z0-9-]%')
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] bigint NOT NULL IDENTITY,
        [BrandId] int NOT NULL,
        [Sku] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Slug] nvarchar(200) NOT NULL,
        [ProductType] nvarchar(20) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [BasePrice] decimal(18,2) NOT NULL,
        [Stock] int NOT NULL DEFAULT 0,
        [IsCustomizable] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Products_BasePrice] CHECK ([BasePrice] > 0),
        CONSTRAINT [CK_Products_ProductType] CHECK ([ProductType] IN (N'Keyboard', N'Mouse', N'Headphones', N'Speakers', N'Mousepad', N'Accessory')),
        CONSTRAINT [CK_Products_Slug] CHECK ([Slug] NOT LIKE N'%[^a-z0-9-]%'),
        CONSTRAINT [CK_Products_Stock] CHECK ([Stock] >= 0),
        CONSTRAINT [FK_Products_Brands_BrandId] FOREIGN KEY ([BrandId]) REFERENCES [Brands] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    CREATE TABLE [CustomizationOptions] (
        [Id] bigint NOT NULL IDENTITY,
        [ProductId] bigint NOT NULL,
        [GroupName] nvarchar(100) NOT NULL,
        [Value] nvarchar(200) NOT NULL,
        [PriceDelta] decimal(18,2) NOT NULL DEFAULT 0.0,
        [IsDefault] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsAvailable] bit NOT NULL DEFAULT CAST(1 AS bit),
        [SortOrder] int NOT NULL DEFAULT 0,
        CONSTRAINT [PK_CustomizationOptions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CustomizationOptions_PriceDelta] CHECK ([PriceDelta] >= 0),
        CONSTRAINT [FK_CustomizationOptions_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductCategories] (
        [ProductId] bigint NOT NULL,
        [CategoryId] int NOT NULL,
        [AddedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [IsFeatured] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_ProductCategories] PRIMARY KEY ([ProductId], [CategoryId]),
        CONSTRAINT [FK_ProductCategories_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductCategories_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductSpecifications] (
        [ProductId] bigint NOT NULL,
        [WeightGrams] int NULL,
        [Dimensions] nvarchar(50) NULL,
        [ConnectionType] nvarchar(20) NOT NULL,
        [Material] nvarchar(100) NULL,
        [Color] nvarchar(50) NULL,
        [WarrantyMonths] int NOT NULL DEFAULT 12,
        CONSTRAINT [PK_ProductSpecifications] PRIMARY KEY ([ProductId]),
        CONSTRAINT [CK_ProductSpecifications_Connection] CHECK ([ConnectionType] IN (N'Wired', N'Wireless', N'Bluetooth', N'Hybrid', N'None')),
        CONSTRAINT [CK_ProductSpecifications_Warranty] CHECK ([WarrantyMonths] BETWEEN 0 AND 60),
        CONSTRAINT [CK_ProductSpecifications_Weight] CHECK ([WeightGrams] > 0),
        CONSTRAINT [FK_ProductSpecifications_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Country', N'CreatedAt', N'CreatedBy', N'Name', N'UpdatedAt', N'UpdatedBy', N'Website') AND [object_id] = OBJECT_ID(N'[Brands]'))
        SET IDENTITY_INSERT [Brands] ON;
    EXEC(N'INSERT INTO [Brands] ([Id], [Country], [CreatedAt], [CreatedBy], [Name], [UpdatedAt], [UpdatedBy], [Website])
    VALUES (1, N''Україна'', ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Keyforge'', NULL, NULL, N''https://keyforge.example.com''),
    (2, N''Польща'', ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Glidex'', NULL, NULL, N''https://glidex.example.com''),
    (3, N''Україна'', ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''SonicCraft'', NULL, NULL, N''https://soniccraft.example.com''),
    (4, N''Україна'', ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''PadArt'', NULL, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Country', N'CreatedAt', N'CreatedBy', N'Name', N'UpdatedAt', N'UpdatedBy', N'Website') AND [object_id] = OBJECT_ID(N'[Brands]'))
        SET IDENTITY_INSERT [Brands] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'CreatedBy', N'Name', N'Slug', N'SortOrder', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] ON;
    EXEC(N'INSERT INTO [Categories] ([Id], [CreatedAt], [CreatedBy], [Name], [Slug], [SortOrder], [UpdatedAt], [UpdatedBy])
    VALUES (1, ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Клавіатури'', N''keyboards'', 1, NULL, NULL),
    (2, ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Миші'', N''mice'', 2, NULL, NULL),
    (3, ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Аудіо'', N''audio'', 3, NULL, NULL),
    (4, ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Килимки'', N''mousepads'', 4, NULL, NULL),
    (5, ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Аксесуари'', N''accessories'', 5, NULL, NULL),
    (6, ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Кастомізація'', N''custom'', 6, NULL, NULL),
    (7, ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Новинки'', N''new'', 7, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'CreatedBy', N'Name', N'Slug', N'SortOrder', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BasePrice', N'BrandId', N'CreatedAt', N'CreatedBy', N'Description', N'IsActive', N'IsCustomizable', N'Name', N'ProductType', N'Sku', N'Slug', N'Stock', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [BasePrice], [BrandId], [CreatedAt], [CreatedBy], [Description], [IsActive], [IsCustomizable], [Name], [ProductType], [Sku], [Slug], [Stock], [UpdatedAt], [UpdatedBy])
    VALUES (CAST(1 AS bigint), 4500.0, 1, ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Механічна клавіатура 75% в алюмінієвому корпусі, gasket mount'', CAST(1 AS bit), CAST(1 AS bit), N''Клавіатура Aurora 75 (алюміній)'', N''Keyboard'', N''KB-075-ALU'', N''aurora-75-alu'', 15, NULL, NULL),
    (CAST(2 AS bigint), 2200.0, 2, ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Легка ігрова миша 58 г, сенсор 26K DPI'', CAST(1 AS bit), CAST(1 AS bit), N''Миша Glide Pro'', N''Mouse'', N''MS-GLD-01'', N''glide-pro'', 30, NULL, NULL),
    (CAST(3 AS bigint), 3800.0, 3, ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Закриті студійні навушники зі змінними амбушурами'', CAST(1 AS bit), CAST(1 AS bit), N''Навушники Studio Wave'', N''Headphones'', N''HP-STU-02'', N''studio-wave'', 12, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BasePrice', N'BrandId', N'CreatedAt', N'CreatedBy', N'Description', N'IsActive', N'IsCustomizable', N'Name', N'ProductType', N'Sku', N'Slug', N'Stock', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BasePrice', N'BrandId', N'CreatedAt', N'CreatedBy', N'Description', N'IsActive', N'Name', N'ProductType', N'Sku', N'Slug', N'Stock', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [BasePrice], [BrandId], [CreatedAt], [CreatedBy], [Description], [IsActive], [Name], [ProductType], [Sku], [Slug], [Stock], [UpdatedAt], [UpdatedBy])
    VALUES (CAST(4 AS bigint), 3100.0, 3, ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Настільна акустика 2.0, 2×15 Вт'', CAST(1 AS bit), N''Колонки DeskBeat 2.0'', N''Speakers'', N''SP-DSK-20'', N''deskbeat-20'', 10, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BasePrice', N'BrandId', N'CreatedAt', N'CreatedBy', N'Description', N'IsActive', N'Name', N'ProductType', N'Sku', N'Slug', N'Stock', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BasePrice', N'BrandId', N'CreatedAt', N'CreatedBy', N'Description', N'IsActive', N'IsCustomizable', N'Name', N'ProductType', N'Sku', N'Slug', N'Stock', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [BasePrice], [BrandId], [CreatedAt], [CreatedBy], [Description], [IsActive], [IsCustomizable], [Name], [ProductType], [Sku], [Slug], [Stock], [UpdatedAt], [UpdatedBy])
    VALUES (CAST(5 AS bigint), 950.0, 4, ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Тканинний килимок з прошитим краєм, можливий власний принт'', CAST(1 AS bit), CAST(1 AS bit), N''Килимок Canvas XL 900×400'', N''Mousepad'', N''MP-XL-900'', N''canvas-xl-900'', 50, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BasePrice', N'BrandId', N'CreatedAt', N'CreatedBy', N'Description', N'IsActive', N'IsCustomizable', N'Name', N'ProductType', N'Sku', N'Slug', N'Stock', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BasePrice', N'BrandId', N'CreatedAt', N'CreatedBy', N'Description', N'IsActive', N'Name', N'ProductType', N'Sku', N'Slug', N'Stock', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [BasePrice], [BrandId], [CreatedAt], [CreatedBy], [Description], [IsActive], [Name], [ProductType], [Sku], [Slug], [Stock], [UpdatedAt], [UpdatedBy])
    VALUES (CAST(6 AS bigint), 1600.0, 1, ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Набір 140 кейкапів PBT, профіль Cherry'', CAST(1 AS bit), N''Набір кейкапів PBT Retro'', N''Accessory'', N''KC-PBT-RETRO'', N''pbt-retro-keycaps'', 25, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BasePrice', N'BrandId', N'CreatedAt', N'CreatedBy', N'Description', N'IsActive', N'Name', N'ProductType', N'Sku', N'Slug', N'Stock', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BasePrice', N'BrandId', N'CreatedAt', N'CreatedBy', N'Description', N'IsActive', N'IsCustomizable', N'Name', N'ProductType', N'Sku', N'Slug', N'Stock', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [BasePrice], [BrandId], [CreatedAt], [CreatedBy], [Description], [IsActive], [IsCustomizable], [Name], [ProductType], [Sku], [Slug], [Stock], [UpdatedAt], [UpdatedBy])
    VALUES (CAST(7 AS bigint), 700.0, 1, ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Кручений кабель з авіаційним роз''''ємом'', CAST(1 AS bit), CAST(1 AS bit), N''Кабель Coil USB-C'', N''Accessory'', N''CB-COIL-USBC'', N''coil-usb-c'', 40, NULL, NULL),
    (CAST(8 AS bigint), 2900.0, 1, ''2026-06-01T00:00:00.0000000Z'', N''seed'', N''Компактна 60% клавіатура з hot-swap роз''''ємами'', CAST(1 AS bit), CAST(1 AS bit), N''Клавіатура Pocket 60 Hot-Swap'', N''Keyboard'', N''KB-060-HS'', N''pocket-60-hs'', 20, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BasePrice', N'BrandId', N'CreatedAt', N'CreatedBy', N'Description', N'IsActive', N'IsCustomizable', N'Name', N'ProductType', N'Sku', N'Slug', N'Stock', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [IsDefault], [ProductId], [SortOrder], [Value])
    VALUES (CAST(1 AS bigint), N''Світчі'', CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bigint), 1, N''Gateron Red'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [PriceDelta], [ProductId], [SortOrder], [Value])
    VALUES (CAST(2 AS bigint), N''Світчі'', CAST(1 AS bit), 900.0, CAST(1 AS bigint), 2, N''Holy Panda'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [IsDefault], [ProductId], [SortOrder], [Value])
    VALUES (CAST(3 AS bigint), N''Кейкапи'', CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bigint), 1, N''PBT Black'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [PriceDelta], [ProductId], [SortOrder], [Value])
    VALUES (CAST(4 AS bigint), N''Кейкапи'', CAST(1 AS bit), 600.0, CAST(1 AS bigint), 2, N''Retro Beige'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [IsDefault], [ProductId], [SortOrder], [Value])
    VALUES (CAST(5 AS bigint), N''Колір корпусу'', CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bigint), 1, N''Silver'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [PriceDelta], [ProductId], [SortOrder], [Value])
    VALUES (CAST(6 AS bigint), N''Колір корпусу'', CAST(1 AS bit), 400.0, CAST(1 AS bigint), 2, N''Midnight Blue'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [IsDefault], [ProductId], [SortOrder], [Value])
    VALUES (CAST(7 AS bigint), N''Колір корпусу'', CAST(1 AS bit), CAST(1 AS bit), CAST(2 AS bigint), 1, N''Black'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [PriceDelta], [ProductId], [SortOrder], [Value])
    VALUES (CAST(8 AS bigint), N''Колір корпусу'', CAST(1 AS bit), 200.0, CAST(2 AS bigint), 2, N''Lilac'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [IsDefault], [ProductId], [SortOrder], [Value])
    VALUES (CAST(9 AS bigint), N''Покриття'', CAST(1 AS bit), CAST(1 AS bit), CAST(2 AS bigint), 1, N''Matte'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [PriceDelta], [ProductId], [SortOrder], [Value])
    VALUES (CAST(10 AS bigint), N''Покриття'', CAST(1 AS bit), 150.0, CAST(2 AS bigint), 2, N''Glossy'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [IsDefault], [ProductId], [SortOrder], [Value])
    VALUES (CAST(11 AS bigint), N''Амбушури'', CAST(1 AS bit), CAST(1 AS bit), CAST(3 AS bigint), 1, N''Velour'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [PriceDelta], [ProductId], [SortOrder], [Value])
    VALUES (CAST(12 AS bigint), N''Амбушури'', CAST(1 AS bit), 450.0, CAST(3 AS bigint), 2, N''Leather''),
    (CAST(13 AS bigint), N''Гравіювання'', CAST(1 AS bit), 300.0, CAST(3 AS bigint), 1, N''Так'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [IsDefault], [ProductId], [SortOrder], [Value])
    VALUES (CAST(14 AS bigint), N''Принт'', CAST(1 AS bit), CAST(1 AS bit), CAST(5 AS bigint), 1, N''Без принту'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [PriceDelta], [ProductId], [SortOrder], [Value])
    VALUES (CAST(15 AS bigint), N''Принт'', CAST(1 AS bit), 350.0, CAST(5 AS bigint), 2, N''Власний принт'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [IsDefault], [ProductId], [SortOrder], [Value])
    VALUES (CAST(16 AS bigint), N''Світчі'', CAST(1 AS bit), CAST(1 AS bit), CAST(8 AS bigint), 1, N''Gateron Yellow'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [PriceDelta], [ProductId], [SortOrder], [Value])
    VALUES (CAST(17 AS bigint), N''Світчі'', CAST(1 AS bit), 500.0, CAST(8 AS bigint), 2, N''Kailh Box White'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [IsDefault], [ProductId], [SortOrder], [Value])
    VALUES (CAST(18 AS bigint), N''Довжина'', CAST(1 AS bit), CAST(1 AS bit), CAST(7 AS bigint), 1, N''1.5 м'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'IsDefault', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] ON;
    EXEC(N'INSERT INTO [CustomizationOptions] ([Id], [GroupName], [IsAvailable], [PriceDelta], [ProductId], [SortOrder], [Value])
    VALUES (CAST(19 AS bigint), N''Довжина'', CAST(1 AS bit), 100.0, CAST(7 AS bigint), 2, N''2 м'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'GroupName', N'IsAvailable', N'PriceDelta', N'ProductId', N'SortOrder', N'Value') AND [object_id] = OBJECT_ID(N'[CustomizationOptions]'))
        SET IDENTITY_INSERT [CustomizationOptions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Brands_Name] ON [Brands] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CustomizationOptions_DefaultPerGroup] ON [CustomizationOptions] ([ProductId], [GroupName]) WHERE [IsDefault] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomizationOptions_ProductId_GroupName_Value] ON [CustomizationOptions] ([ProductId], [GroupName], [Value]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductCategories_CategoryId_AddedAt] ON [ProductCategories] ([CategoryId], [AddedAt] DESC) INCLUDE ([IsFeatured]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_BrandId] ON [Products] ([BrandId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Products_Name] ON [Products] ([Name]) INCLUDE ([Slug], [BasePrice]) WHERE [IsActive] = 1 AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Products_ProductType_BasePrice] ON [Products] ([ProductType], [BasePrice]) INCLUDE ([Name], [Slug], [BrandId], [IsCustomizable]) WHERE [IsActive] = 1 AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_Sku] ON [Products] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_Slug] ON [Products] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023202845_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261023202845_InitialCreate', N'9.0.20');
END;

COMMIT;
GO


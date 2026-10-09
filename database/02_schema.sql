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
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE TABLE [AbpRoles] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(32) NOT NULL,
        [DisplayName] nvarchar(64) NOT NULL,
        [CreationTime] datetime2 NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_AbpRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE TABLE [AbpUsers] (
        [Id] bigint NOT NULL IDENTITY,
        [UserName] nvarchar(256) NOT NULL,
        [Name] nvarchar(64) NOT NULL,
        [EmailAddress] nvarchar(256) NOT NULL,
        [Password] nvarchar(128) NOT NULL,
        [PhoneNumber] nvarchar(32) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreationTime] datetime2 NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_AbpUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(255) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreationTime] datetime2 NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE TABLE [Materials] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(255) NOT NULL,
        [Origin] nvarchar(255) NULL,
        [Description] nvarchar(max) NULL,
        [CreationTime] datetime2 NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Materials] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE TABLE [AbpUserRoles] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [RoleId] int NOT NULL,
        [CreationTime] datetime2 NOT NULL,
        CONSTRAINT [PK_AbpUserRoles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AbpUserRoles_AbpRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AbpRoles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AbpUserRoles_AbpUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AbpUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE TABLE [Carts] (
        [Id] int NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [CreationTime] datetime2 NOT NULL,
        CONSTRAINT [PK_Carts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Carts_AbpUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AbpUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] int NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [ReceiverName] nvarchar(255) NOT NULL,
        [PhoneNumber] varchar(20) NOT NULL,
        [ShippingAddress] nvarchar(500) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [PaymentMethod] int NOT NULL,
        [Note] nvarchar(500) NULL,
        [Status] int NOT NULL,
        [CreationTime] datetime2 NOT NULL,
        [LastStatusChangeTime] datetime2 NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Orders_AbpUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AbpUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE TABLE [UserAddresses] (
        [Id] int NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [ReceiverName] nvarchar(255) NOT NULL,
        [PhoneNumber] varchar(20) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [IsDefault] bit NOT NULL,
        [CreationTime] datetime2 NOT NULL,
        CONSTRAINT [PK_UserAddresses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserAddresses_AbpUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AbpUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [CategoryId] int NOT NULL,
        [MaterialId] int NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [MainImage] nvarchar(500) NOT NULL,
        [Description] nvarchar(max) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreationTime] datetime2 NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Products_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materials] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductImages] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [ImageUrl] nvarchar(500) NOT NULL,
        [DisplayOrder] int NOT NULL,
        CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductVariants] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [Size] nvarchar(100) NULL,
        [Style] nvarchar(100) NULL,
        [StockQuantity] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_ProductVariants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductVariants_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE TABLE [CartItems] (
        [Id] int NOT NULL IDENTITY,
        [CartId] int NOT NULL,
        [ProductVariantId] int NOT NULL,
        [Quantity] int NOT NULL,
        CONSTRAINT [PK_CartItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CartItems_Carts_CartId] FOREIGN KEY ([CartId]) REFERENCES [Carts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CartItems_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderItems] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [ProductVariantId] int NOT NULL,
        [ProductName] nvarchar(255) NOT NULL,
        [VariantName] nvarchar(255) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderItems_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AbpRoles_Name] ON [AbpRoles] ([Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AbpUserRoles_RoleId] ON [AbpUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AbpUserRoles_UserId_RoleId] ON [AbpUserRoles] ([UserId], [RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AbpUsers_EmailAddress] ON [AbpUsers] ([EmailAddress]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AbpUsers_UserName] ON [AbpUsers] ([UserName]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CartItems_CartId_ProductVariantId] ON [CartItems] ([CartId], [ProductVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CartItems_ProductVariantId] ON [CartItems] ([ProductVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Carts_UserId] ON [Carts] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Categories_Name] ON [Categories] ([Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Materials_Name] ON [Materials] ([Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderItems_ProductVariantId] ON [OrderItems] ([ProductVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_Status_CreationTime] ON [Orders] ([Status], [CreationTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_UserId_CreationTime] ON [Orders] ([UserId], [CreationTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductImages_ProductId] ON [ProductImages] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_IsActive_CategoryId] ON [Products] ([IsActive], [CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_MaterialId] ON [Products] ([MaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductVariants_ProductId] ON [ProductVariants] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserAddresses_UserId] ON [UserAddresses] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006031334_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006031334_InitialCreate', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006103643_AddPasswordResetTokens'
)
BEGIN
    CREATE TABLE [PasswordResetTokens] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [TokenHash] char(64) NOT NULL,
        [ExpirationTime] datetime2 NOT NULL,
        [UsedTime] datetime2 NULL,
        [CreationTime] datetime2 NOT NULL,
        CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PasswordResetTokens_AbpUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AbpUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006103643_AddPasswordResetTokens'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PasswordResetTokens_TokenHash] ON [PasswordResetTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006103643_AddPasswordResetTokens'
)
BEGIN
    CREATE INDEX [IX_PasswordResetTokens_UserId_CreationTime] ON [PasswordResetTokens] ([UserId], [CreationTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006103643_AddPasswordResetTokens'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006103643_AddPasswordResetTokens', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007021638_AddUserSecurityStamp'
)
BEGIN
    ALTER TABLE [AbpUsers] ADD [SecurityStamp] varchar(32) NOT NULL DEFAULT (REPLACE(CONVERT(char(36), NEWID()), '-', ''));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007021638_AddUserSecurityStamp'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007021638_AddUserSecurityStamp', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009084805_AddStockReceiptsAndCost'
)
BEGIN
    ALTER TABLE [ProductVariants] ADD [AverageCost] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009084805_AddStockReceiptsAndCost'
)
BEGIN
    ALTER TABLE [OrderItems] ADD [UnitCost] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009084805_AddStockReceiptsAndCost'
)
BEGIN
    CREATE TABLE [StockReceipts] (
        [Id] int NOT NULL IDENTITY,
        [ProductVariantId] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitCost] decimal(18,2) NOT NULL,
        [Note] nvarchar(500) NULL,
        [StockBefore] int NOT NULL,
        [AverageCostBefore] decimal(18,4) NULL,
        [CreatorUserId] bigint NOT NULL,
        [CreationTime] datetime2 NOT NULL,
        [LastModificationTime] datetime2 NULL,
        [LastModifierUserId] bigint NULL,
        CONSTRAINT [PK_StockReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockReceipts_AbpUsers_CreatorUserId] FOREIGN KEY ([CreatorUserId]) REFERENCES [AbpUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockReceipts_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009084805_AddStockReceiptsAndCost'
)
BEGIN
    CREATE INDEX [IX_StockReceipts_CreationTime] ON [StockReceipts] ([CreationTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009084805_AddStockReceiptsAndCost'
)
BEGIN
    CREATE INDEX [IX_StockReceipts_CreatorUserId] ON [StockReceipts] ([CreatorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009084805_AddStockReceiptsAndCost'
)
BEGIN
    CREATE INDEX [IX_StockReceipts_ProductVariantId_Id] ON [StockReceipts] ([ProductVariantId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009084805_AddStockReceiptsAndCost'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009084805_AddStockReceiptsAndCost', N'10.0.9');
END;

COMMIT;
GO


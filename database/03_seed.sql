-- Dữ liệu khởi tạo cho HonSungVietDb: vai trò, tài khoản quản trị / nhân viên demo, danh mục, nguyên liệu, sản phẩm mẫu.
-- Chỉ chạy khi database còn trống (chưa có vai trò nào). Không chứa dữ liệu khách hàng.
-- Tài khoản demo: admin / 123qwe (Quản trị viên), staff / 123qwe (Nhân viên) — đổi mật khẩu trước khi triển khai.
-- Mật khẩu lưu dạng hash PBKDF2 của ASP.NET Core Identity (PasswordHasher), dùng được trên mọi máy.
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- Kiểm tra và chèn dữ liệu nằm trong CÙNG một batch (không có GO ở giữa) để RETURN dừng toàn bộ phần chèn.
IF EXISTS (SELECT 1 FROM [AbpRoles])
BEGIN
    PRINT N'Database đã có dữ liệu, bỏ qua seed.';
    RETURN;
END

BEGIN TRANSACTION;

DECLARE @now datetime2 = SYSDATETIME();

-- Vai trò
SET IDENTITY_INSERT [AbpRoles] ON;
INSERT INTO [AbpRoles] ([Id], [Name], [DisplayName], [CreationTime], [IsDeleted]) VALUES
    (1, N'Admin',    N'Quản trị viên', @now, 0),
    (2, N'Staff',    N'Nhân viên',     @now, 0),
    (3, N'Customer', N'Khách hàng',    @now, 0);
SET IDENTITY_INSERT [AbpRoles] OFF;

-- Tài khoản demo (mật khẩu: 123qwe)
SET IDENTITY_INSERT [AbpUsers] ON;
INSERT INTO [AbpUsers] ([Id], [UserName], [Name], [EmailAddress], [Password], [PhoneNumber], [IsActive], [CreationTime], [IsDeleted]) VALUES
    (1, N'admin', N'Quản trị viên', N'admin@honsungviet.local',
        N'AQAAAAIAAYagAAAAEHBuEwPDoLT1z3NZ3KlUkylDR1DbnU6k5g9Yz02N9VD6NJ46gePxd5YQSIqutLoHkA==', NULL, 1, @now, 0),
    (2, N'staff', N'Nhân viên', N'staff@honsungviet.local',
        N'AQAAAAIAAYagAAAAEO1fkIXT4oX4uXgq4L0R7WanK3Hz1nKcN1DEMokbutHLYxCV897PKtgAaDWhHJz1kg==', NULL, 1, @now, 0);
SET IDENTITY_INSERT [AbpUsers] OFF;

INSERT INTO [AbpUserRoles] ([UserId], [RoleId], [CreationTime]) VALUES
    (1, 1, @now),
    (2, 2, @now);

-- Danh mục
SET IDENTITY_INSERT [Categories] ON;
INSERT INTO [Categories] ([Id], [Name], [Description], [CreationTime], [IsDeleted]) VALUES
    (1, N'Lược sừng',      NULL, @now, 0),
    (2, N'Trâm cài',       NULL, @now, 0),
    (3, N'Vòng tay, nhẫn', NULL, @now, 0),
    (4, N'Đồ gia dụng',    NULL, @now, 0),
    (5, N'Đồ trang trí',   NULL, @now, 0),
    (6, N'Móc khóa',       NULL, @now, 0);
SET IDENTITY_INSERT [Categories] OFF;

-- Nguyên liệu và nguồn gốc
SET IDENTITY_INSERT [Materials] ON;
INSERT INTO [Materials] ([Id], [Name], [Origin], [Description], [CreationTime], [IsDeleted]) VALUES
    (1, N'Sừng trâu', N'Làng nghề Thụy Ứng, Hà Nội',
        N'Sừng trâu tự nhiên thu từ trâu nuôi lấy thịt, xử lý và mài thủ công.', @now, 0),
    (2, N'Sừng bò', N'Thu mua trong nước',
        N'Sừng bò tự nhiên, vân sáng màu, đã qua xử lý khử mùi.', @now, 0);
SET IDENTITY_INSERT [Materials] OFF;

-- Sản phẩm mẫu
SET IDENTITY_INSERT [Products] ON;
INSERT INTO [Products] ([Id], [CategoryId], [MaterialId], [Name], [Price], [MainImage], [Description], [IsActive], [CreationTime], [IsDeleted]) VALUES
    (1, 1, 1, N'Lược sừng trâu chạm khắc', 180000, N'/uploads/sample/placeholder.svg', N'Lược sừng trâu chạm khắc chế tác thủ công từ sừng trâu.', 1, @now, 0),
    (2, 1, 1, N'Lược sừng trâu răng thưa', 120000, N'/uploads/sample/placeholder.svg', N'Lược sừng trâu răng thưa chế tác thủ công từ sừng trâu.', 1, @now, 0),
    (3, 1, 2, N'Lược sừng bò bỏ túi',       90000, N'/uploads/sample/placeholder.svg', N'Lược sừng bò bỏ túi chế tác thủ công từ sừng bò.', 1, @now, 0),
    (4, 2, 1, N'Trâm cài sừng hoa sen',    150000, N'/uploads/sample/placeholder.svg', N'Trâm cài sừng hoa sen chế tác thủ công từ sừng trâu.', 1, @now, 0),
    (5, 3, 1, N'Vòng tay sừng trơn',       250000, N'/uploads/sample/placeholder.svg', N'Vòng tay sừng trơn chế tác thủ công từ sừng trâu.', 1, @now, 0),
    (6, 4, 2, N'Thìa sừng ăn cơm',          45000, N'/uploads/sample/placeholder.svg', N'Thìa sừng ăn cơm chế tác thủ công từ sừng bò.', 1, @now, 0),
    (7, 6, 1, N'Móc khóa sừng hình cá',     60000, N'/uploads/sample/placeholder.svg', N'Móc khóa sừng hình cá chế tác thủ công từ sừng trâu.', 1, @now, 0);
SET IDENTITY_INSERT [Products] OFF;

-- Biến thể và tồn kho (RowVersion do SQL Server tự sinh)
SET IDENTITY_INSERT [ProductVariants] ON;
INSERT INTO [ProductVariants] ([Id], [ProductId], [Size], [Style], [StockQuantity], [IsDeleted]) VALUES
    (1,  1, N'Nhỏ',  N'Răng thưa', 12, 0),
    (2,  1, N'Vừa',  N'Răng thưa',  8, 0),
    (3,  1, N'Lớn',  N'Răng dày',   0, 0),
    (4,  2, N'Vừa',  NULL,         20, 0),
    (5,  3, N'Mini', NULL,          3, 0),
    (6,  4, NULL,    N'Hoa sen',    6, 0),
    (7,  4, NULL,    N'Chim lạc',   4, 0),
    (8,  5, N'Cỡ 56', N'Trơn',      5, 0),
    (9,  5, N'Cỡ 58', N'Trơn',      7, 0),
    (10, 5, N'Cỡ 60', N'Trơn',      2, 0),
    (11, 6, NULL,    NULL,         40, 0),
    (12, 7, NULL,    N'Cá chép',   15, 0);
SET IDENTITY_INSERT [ProductVariants] OFF;

COMMIT TRANSACTION;
PRINT N'Đã khởi tạo dữ liệu mẫu.';
GO

-- Tạo database HonSungVietDb (chạy một lần, an toàn khi chạy lại).
-- Collation Latin1_General_100_CI_AI: tìm kiếm không phân biệt hoa thường và dấu ("luoc sung" tìm ra "Lược sừng").
-- Trùng với b.UseCollation("Latin1_General_100_CI_AI") trong HonSungVietDbContext.
USE [master];
GO

IF DB_ID(N'HonSungVietDb') IS NULL
BEGIN
    CREATE DATABASE [HonSungVietDb] COLLATE Latin1_General_100_CI_AI;
END
GO

USE [HonSungVietDb];
GO

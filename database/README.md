# Database HonSungVietDb

Ba script tạo lại toàn bộ database trên SQL Server 2019 trở lên, không cần chạy ứng dụng.

| File | Nội dung |
|---|---|
| `01_create_database.sql` | Tạo database `HonSungVietDb`, collation `Latin1_General_100_CI_AI` |
| `02_schema.sql` | 14 bảng (kể cả `PasswordResetTokens` cho Quên mật khẩu) + khóa, chỉ mục (sinh từ EF Core migration, chạy lại an toàn) |
| `03_seed.sql` | Vai trò, tài khoản demo, 6 danh mục, 2 nguyên liệu, 7 sản phẩm, 12 biến thể (bỏ qua nếu đã có dữ liệu) |

## Cách 1 — SQL Server Management Studio

Mở và chạy (F5) lần lượt `01` → `02` → `03`. Từ script `02`, chọn database `HonSungVietDb` ở ô chọn database trên thanh công cụ.

## Cách 2 — dòng lệnh

```powershell
cd database
sqlcmd -S localhost -E -C -b -I -f 65001 -i 01_create_database.sql
sqlcmd -S localhost -E -C -b -I -f 65001 -d HonSungVietDb -i 02_schema.sql
sqlcmd -S localhost -E -C -b -I -f 65001 -d HonSungVietDb -i 03_seed.sql
```

- `-I` bắt buộc: bật `QUOTED_IDENTIFIER`, cần cho các chỉ mục có điều kiện (`WHERE [IsDeleted] = 0`).
- `-f 65001` để đọc đúng tiếng Việt (UTF-8).
- Đổi `localhost` thành tên instance nếu cần, VD `.\SQLEXPRESS`.

## Cách 3 — để ứng dụng tự tạo

Ở môi trường Development (`Database:MigrateOnStartup = true`), chạy `dotnet run --project Website.Api` là tự tạo database, áp migration và seed cùng dữ liệu như trên.

## Tài khoản demo

| Tài khoản | Mật khẩu | Vai trò |
|---|---|---|
| `admin` | `123qwe` | Quản trị viên |
| `staff` | `123qwe` | Nhân viên |

Đổi mật khẩu trước khi triển khai thật. Seed không tạo tài khoản khách hàng.

## Ghi chú thiết kế

- **Collation `Latin1_General_100_CI_AI`**: không phân biệt hoa thường và dấu, kể cả ư/ơ/đ, nên tìm "luoc sung" ra "Lược sừng", "do trang tri" ra "Đồ trang trí". `Vietnamese_CI_AI` không làm được điều này vì coi ư, ơ, đ là chữ cái riêng.
- **Bảng người dùng** giữ tên `AbpUsers`, `AbpRoles`, `AbpUserRoles` theo thiết kế CSDL mục 3.6.
- **`ProductVariants.RowVersion`** (rowversion): khóa lạc quan, chống bán vượt tồn kho khi nhiều khách đặt cùng lúc.
- **Xóa mềm** (`IsDeleted`) cho danh mục, nguyên liệu, sản phẩm, biến thể, người dùng; các chỉ mục duy nhất (tên, email) chỉ áp dụng cho bản ghi chưa xóa.

Khi đổi model, sinh lại `02_schema.sql`:

```powershell
dotnet ef migrations add <TenMigration> -p Website.Infrastructure -s Website.Api -o Persistence/Migrations
dotnet ef migrations script --idempotent -p Website.Infrastructure -s Website.Api -o database/02_schema.sql
```

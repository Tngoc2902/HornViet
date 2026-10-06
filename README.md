# Hồn Sừng Việt — Backend

Web API cho website thương mại điện tử bán đồ mỹ nghệ từ sừng (đồ án tốt nghiệp).
Tài liệu API cho frontend: [docs/API.md](docs/API.md).

## Kiến trúc

Cấu trúc DDD theo chuẩn của pixelz-api:

```
SharedKernel/                    Khối dựng dùng chung: Entity<TKey>, ValueObject, IHasCreationTime, ISoftDelete, UserFriendlyException
Website.Domain/                  Tầng Miền
  {Module}/Entities/             Catalog, Carts, Orders, Users — entity + quy tắc nghiệp vụ (tồn kho theo biến thể, máy trạng thái đơn)
  Interfaces/Repositories/       IRepository<TEntity, TKey>, IUnitOfWork
Website.Application/             Tầng Ứng dụng: Application Service + DTO cho từng use case, DataSeeder
  Common/Interfaces/             IAppSession
Website.Infrastructure/          Tầng Hạ tầng
  Persistence/                   WebsiteDbContext, Migrations/, Repositories/
  DependencyInjectionRegister.cs AddInfrastructure(configuration)
Website.Api/                     Web API
  Controllers/{Module}/          Account, Catalog, Shopping, Admin (kế thừa WebsiteBaseController)
  Models/                        AjaxResponse + model riêng của API
  Authorization/                 JWT, phân quyền, IAppSession từ token
  Middlewares/                   Bọc response, xử lý lỗi, lỗi validation
tests/                           Unit test
```

Phụ thuộc: `Api → Application → Domain → SharedKernel`, `Api → Infrastructure → Domain`.

Điểm thiết kế chính:

- **Sản phẩm – Biến thể – Tồn kho**: giỏ và đơn tham chiếu `ProductVariantId`; tồn kho chỉ đổi qua `ProductVariant.DecreaseStock / IncreaseStock / SetStock`.
- **Giao dịch**: mỗi use case ghi CSDL bằng một lần `SaveChanges` → tạo đơn + trừ kho + xóa giỏ cùng thành công hoặc cùng hoàn tác.
- **Chống bán vượt tồn kho**: cột `RowVersion` (khóa lạc quan) trên `ProductVariants`; giao dịch đến sau nhận 409.
- **Xóa mềm** (`IsDeleted` + query filter toàn cục) cho danh mục, nguyên liệu, sản phẩm, biến thể, người dùng.
- **Lịch sử đơn bất biến**: `OrderItems` lưu tên sản phẩm, tên biến thể và đơn giá tại thời điểm mua.
- **Định dạng API tương thích ABP**: route `/api/services/app/{Service}/{Action}`, response `{ result, success, error, unauthorizedRequest, __abp }`, phân trang `skipCount / maxResultCount`; bảng người dùng `AbpUsers / AbpRoles / AbpUserRoles` như thiết kế CSDL.

## Chạy dự án

Yêu cầu: .NET SDK 10, SQL Server 2019 trở lên.

Database: chạy 3 script trong [database/](database/README.md) (SSMS hoặc sqlcmd), hoặc để ứng dụng tự tạo như dưới đây.

```bash
dotnet tool restore
dotnet run --project Website.Api --launch-profile https
# Swagger: https://localhost:44311/swagger
```

Ở môi trường Development, ứng dụng tự áp dụng migration và khởi tạo dữ liệu (`Database:MigrateOnStartup`):
3 vai trò, tài khoản demo `admin / 123qwe` và `staff / 123qwe`, danh mục / nguyên liệu / sản phẩm mẫu.
Chuỗi kết nối dev nằm trong `Website.Api/appsettings.Development.json` (`Server=localhost`, đăng nhập Windows).

Thêm migration khi đổi model:

```bash
dotnet ef migrations add <Ten> -p Website.Infrastructure -s Website.Api -o Persistence/Migrations
```

## Triển khai

- Đặt `Jwt__SecurityKey` (≥ 32 ký tự) và `ConnectionStrings__Default` qua biến môi trường; `appsettings.json` cố ý để trống khóa JWT nên ứng dụng không khởi động nếu thiếu.
- Đổi mật khẩu tài khoản demo (`Seed:AdminPassword`, `Seed:StaffPassword`) hoặc đổi ngay sau lần chạy đầu.
- Cập nhật `App:CorsOrigins` theo domain frontend.

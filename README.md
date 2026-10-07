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

Frontend gồm hai repo gọi chung backend này; `App:CorsOrigins` trong `Website.Api/appsettings.json` đã có sẵn cả hai origin
(CORS chỉ nạp lúc khởi động — sửa xong phải chạy lại backend):

| Repo | Dành cho | Địa chỉ dev | Đăng nhập qua |
|---|---|---|---|
| `HornViet_customer` | Khách vãng lai, Khách hàng | `http://localhost:3000` | `POST /api/TokenAuth/Authenticate` |
| `HornViet_admin` | Nhân viên, Quản trị viên | `http://localhost:3001` | `POST /api/TokenAuth/AuthenticateAdmin` |

### Quên mật khẩu và email

Link đặt lại mật khẩu trong email trỏ về `App:ClientRootAddress` (Khách hàng) hoặc `App:AdminRootAddress`
(Nhân viên / Quản trị viên). Cấu hình gửi mail ở mục `Smtp`:

- **Dev, chưa có tài khoản email** (`Smtp:Host` để trống — mặc định): email không gửi đi mà được lưu thành file `.eml`
  trong `Website.Api/App_Data/mails/` (log ghi đường dẫn). Mở file bằng Outlook / Thunderbird hoặc trình soạn thảo để lấy link.
- **Gửi thật** (VD Gmail, cần bật xác minh 2 bước và tạo App Password): đặt qua user-secrets hoặc biến môi trường,
  không ghi mật khẩu vào `appsettings.json`:

```bash
dotnet user-secrets set "Smtp:Host" "smtp.gmail.com" --project Website.Api
```
```bash
dotnet user-secrets set "Smtp:UserName" "ten-ban@gmail.com" --project Website.Api
```
```bash
dotnet user-secrets set "Smtp:Password" "<app-password-16-ky-tu>" --project Website.Api
```
```bash
dotnet user-secrets set "Smtp:FromAddress" "ten-ban@gmail.com" --project Website.Api
```

### Debug bằng Visual Studio

Lỗi nghiệp vụ (sai mật khẩu, hết hàng, …) được ném dưới dạng `UserFriendlyException` / `EntityNotFoundException` và
middleware đổi thành HTTP 400 / 404. Khi chạy bằng **F5**, Visual Studio mặc định dừng ở các exception này
("Exception User-Unhandled") trước khi middleware trả response, nên frontend phải chờ bạn bấm Continue. Đây là cài đặt
của trình gỡ lỗi, không phải lỗi ứng dụng (`dotnet run` hoặc **Ctrl+F5** không bị). Để tắt, khi hộp thoại hiện ra lần đầu:
bỏ chọn **"Break when this exception type is user-unhandled"** rồi bấm Continue — làm một lần cho mỗi loại exception.
Hoặc mở **Debug → Windows → Exception Settings** (Ctrl+Alt+E), thêm hai kiểu
`Website.Domain.SharedKernel.UserFriendlyException` và `Website.Domain.SharedKernel.EntityNotFoundException` vào
*Common Language Runtime Exceptions*, chuột phải từng kiểu → bỏ chọn **Break When User-Unhandled**.

Thêm migration khi đổi model:

```bash
dotnet ef migrations add <Ten> -p Website.Infrastructure -s Website.Api -o Persistence/Migrations
```

## Triển khai

- Từ migration `AddUserSecurityStamp`, token chứa dấu bảo mật của tài khoản; token phát hành trước đó không còn hợp lệ,
  nên sau lần cập nhật đầu tiên mọi người dùng phải đăng nhập lại một lần. Đổi / đặt lại mật khẩu hoặc đổi vai trò
  đăng xuất mọi phiên của tài khoản đó ngay lập tức.

- Đặt `Jwt__SecurityKey` (≥ 32 ký tự) và `ConnectionStrings__Default` qua biến môi trường; `appsettings.json` cố ý để trống khóa JWT nên ứng dụng không khởi động nếu thiếu.
- Đổi mật khẩu tài khoản demo (`Seed:AdminPassword`, `Seed:StaffPassword`) hoặc đổi ngay sau lần chạy đầu.
- Đặt `App__CorsOrigins` chứa tên miền thật của **cả hai** frontend, cách nhau bằng dấu phẩy, VD
  `App__CorsOrigins=https://honsungviet.vn,https://admin.honsungviet.vn`. Dùng HTTPS cho cả backend lẫn hai frontend
  (trang HTTPS không gọi được API HTTP). Ảnh `/uploads/...` phục vụ qua thẻ `<img>` nên không cần CORS.
- Đặt `App__ClientRootAddress` và `App__AdminRootAddress` là tên miền thật của hai frontend (dùng cho link trong email),
  và cấu hình `Smtp__Host`, `Smtp__UserName`, `Smtp__Password`, `Smtp__FromAddress` — nếu để trống `Smtp__Host`,
  email Quên mật khẩu chỉ được lưu thành file trên máy chủ chứ không gửi đi.

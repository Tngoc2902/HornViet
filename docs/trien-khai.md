# Triển khai Hồn Sừng Việt lên Internet (staging + production)

Tài liệu này mô tả cách đưa cả hệ thống (backend `HornViet`, trang cửa hàng `HornViet_customer`, trang quản trị
`HornViet_admin`) lên các dịch vụ **có gói miễn phí**, với hai môi trường tách biệt:

- **staging** — môi trường thử: kiểm tra tính năng mới trước khi cho khách dùng. Database và thư mục ảnh riêng.
- **production** — môi trường thật cho khách hàng.

## 1. Kiến trúc triển khai

| Thành phần | Dịch vụ | Ghi chú |
|---|---|---|
| Trang cửa hàng (Next.js) | **Vercel** | HTTPS tự động, CDN toàn cầu |
| Trang quản trị (Next.js) | **Vercel** | Project riêng |
| Backend .NET 10 + SignalR | **Render** (Docker, vùng Singapore) | Hỗ trợ WebSocket (`wss://`), HTTPS tự động — cấu hình trong `render.yaml`, `Dockerfile` |
| Database | **Neon** — PostgreSQL (gói Free, vùng Singapore) | Nhánh CSDL `main` cho production, `staging` cho staging. Máy phát triển vẫn dùng SQL Server — chọn bằng `Database:Provider` |
| Ảnh sản phẩm, nguyên liệu | **Cloudinary** | Ổ đĩa của Render bị xóa mỗi lần deploy nên ảnh không lưu trên máy chủ |

```
Trình duyệt ──HTTPS──> Vercel (cửa hàng / quản trị)
     │
     └──HTTPS / WSS──> Render (HornViet.Api: REST + Hub /hubs/notifications)
                          ├──TLS 5432──> Neon (PostgreSQL)
                          └──HTTPS─────> Cloudinary (lưu ảnh)  ·  Google (kiểm tra đăng nhập Google)
```

Giới hạn của gói miễn phí cần biết:

- **Render free**: tự "ngủ" sau 15 phút không có request; lần truy cập đầu tiên sau đó chờ khoảng 1 phút. Kết nối
  real-time tự nối lại khi server thức dậy.
- **Neon Free**: giới hạn dung lượng và giờ chạy mỗi tháng (xem trang giá của Neon); khi rảnh database tự tạm dừng,
  lần kết nối đầu chậm vài giây (EF Core đã bật tự thử lại — `EnableRetryOnFailure`). Không cần thẻ thanh toán.

### Hai hệ quản trị CSDL

Cùng một mã nguồn chạy được trên cả hai, chọn bằng cấu hình `Database:Provider`:

| | Máy phát triển | Staging / production |
|---|---|---|
| `Database:Provider` | `SqlServer` (mặc định) | `PostgreSql` |
| DbContext | `HornVietDbContext` | `PostgreSqlHornVietDbContext` (lớp con, chỉ ghi đè phần phụ thuộc CSDL) |
| Migration | `Persistence/Migrations` | `Persistence/PostgreSql/Migrations` |
| Tìm kiếm không dấu | collation `Latin1_General_100_CI_AI` | `lower(unaccent(...))` qua `SearchText.Fold` |
| Khóa lạc quan tồn kho | cột `rowversion` | cột hệ thống `xmin` |

Khi thay đổi entity phải tạo migration cho **cả hai**:

```bash
dotnet ef migrations add TenMigration --context HornVietDbContext --project HornViet.Infrastructure --startup-project HornViet.Api
```

```bash
Database__Provider=PostgreSql ConnectionStrings__Default="Host=localhost;Database=design" dotnet ef migrations add TenMigration --context PostgreSqlHornVietDbContext --output-dir Persistence/PostgreSql/Migrations --project HornViet.Infrastructure --startup-project HornViet.Api
```
- **Vercel Hobby**: dùng cho dự án cá nhân / phi thương mại.

## 2. Nhánh git và quy trình đưa code lên

Cả ba repo dùng cùng mô hình nhánh:

| Nhánh | Vai trò | Tự động deploy tới |
|---|---|---|
| `main` | Nhánh phát triển — nơi merge các nhánh tính năng | (không deploy) |
| `staging` | Bản chờ kiểm tra | Render `hornviet-api-staging`, Vercel Preview của nhánh `staging` |
| `production` | Bản đang phục vụ khách | Render `hornviet-api`, Vercel Production |

Quy trình:

1. Làm tính năng trên nhánh riêng (VD `feature/...`) → Pull Request vào `main`.
2. Muốn thử trên Internet: Pull Request **`main` → `staging`** → merge → staging tự deploy → kiểm tra.
3. Ổn định: Pull Request **`staging` → `production`** → merge → production tự deploy.
4. Không commit thẳng lên `staging` / `production` (có thể bật *Branch protection* trên GitHub để bắt buộc qua PR).

Khi cần đưa migration CSDL mới lên: chỉ cần merge như trên — backend chạy `Database:MigrateOnStartup=true` nên tự áp
migration lúc khởi động.

## 3. Các bước thiết lập (làm một lần)

> Tất cả bí mật (mật khẩu database, khóa Cloudinary, mật khẩu admin…) **chỉ nhập trên trang quản lý của Render /
> Vercel**, không ghi vào repo.

### Bước 1 — Database PostgreSQL trên Neon

1. Đăng ký https://neon.tech → **Continue with GitHub** (không cần thẻ).
2. Tạo project: *Project name* `hornviet`, *Postgres version* **17**, *Region* **AWS Asia Pacific (Singapore)** (cùng
   vùng với Render).
3. Nhánh CSDL mặc định `main` dùng cho **production**. Vào **Branches → Create branch** → tên `staging`, tạo từ `main`.
4. Lấy chuỗi kết nối cho từng nhánh: **Connect** → chọn *Branch* (`main` hoặc `staging`) → **tắt Connection pooling**
   → copy chuỗi dạng `postgresql://...@ep-....ap-southeast-1.aws.neon.tech/neondb?sslmode=require...` (có mật khẩu — bí mật).
   Backend tự chuyển dạng URI này sang dạng của Npgsql, dán nguyên chuỗi là được.
5. Không cần tạo bảng: backend tự áp migration (`Database:MigrateOnStartup=true`) và cài extension `unaccent` khi khởi động.

### Bước 2 — Cloudinary

1. Đăng ký https://cloudinary.com (gói Free).
2. Dashboard → **API Keys / Product environment** → copy **API environment variable** dạng
   `cloudinary://API_KEY:API_SECRET@CLOUD_NAME` (đây là bí mật).
3. Không cần tạo thư mục: backend tự lưu vào `hornviet-staging/` (staging) và `hornviet/` (production).

### Bước 3 — Render (backend)

1. Đăng ký https://render.com bằng tài khoản GitHub, cho phép truy cập repo `HornViet`.
2. **New → Blueprint** → chọn repo `HornViet` → Render đọc `render.yaml` và đề xuất 2 service:
   `hornviet-api-staging` (nhánh `staging`) và `hornviet-api` (nhánh `production`).
3. Nhập các biến được hỏi (xem bảng mục 4). `Jwt__SecurityKey` Render tự sinh ngẫu nhiên cho từng service.
   Lúc này chưa có địa chỉ Vercel: tạm điền `App__*` bằng `https://example.com`, quay lại sửa ở Bước 5.
4. **Apply**. Địa chỉ backend có dạng `https://hornviet-api-staging.onrender.com`, `https://hornviet-api.onrender.com`
   (nếu tên đã có người dùng, Render thêm hậu tố — xem trên trang service).
5. Kiểm tra: mở `https://<địa chỉ backend>/health` → `Healthy`.
6. Nếu lần deploy đầu lỗi vì chưa có chuỗi kết nối: điền `ConnectionStrings__Default` rồi **Manual Deploy → Deploy latest
   commit**.

### Bước 4 — Vercel (hai frontend)

Làm cho từng repo `HornViet_customer` và `HornViet_admin`:

1. Đăng ký https://vercel.com bằng GitHub → **Add New → Project** → import repo. Framework: Next.js (tự nhận).
2. **Settings → Git → Production Branch** = `production`.
3. **Settings → Environment Variables** (xem bảng mục 4):
   - Môi trường **Production**: giá trị của production.
   - Môi trường **Preview**, chọn **branch `staging`**: giá trị của staging.
4. **Deployments** → deploy lại nhánh `production` và `staging` để nhận biến môi trường.
5. Địa chỉ:
   - Production: `https://<tên project>.vercel.app` (có thể gắn tên miền riêng ở *Settings → Domains*).
   - Staging: `https://<tên project>-git-staging-<tên tài khoản>.vercel.app` (địa chỉ cố định của nhánh `staging`,
     xem ở *Deployments*). Preview mặc định yêu cầu đăng nhập Vercel mới xem được (*Deployment Protection*) — tắt ở
     *Settings → Deployment Protection* nếu muốn người khác thử staging.

### Bước 5 — Nối các địa chỉ với nhau

1. Render → từng service → **Environment**: sửa `App__ClientRootAddress`, `App__AdminRootAddress`, `App__CorsOrigins`
   theo địa chỉ Vercel thật của môi trường đó → **Save** (Render tự deploy lại).
2. Google Cloud Console → project `hornviet` → **Google Auth Platform → Clients** → client web → thêm
   **Authorized JavaScript origins**: địa chỉ cửa hàng staging và production (không có `/` ở cuối).
3. Google Auth Platform → **Audience → Publish app** để mọi tài khoản Gmail đăng nhập được (chế độ *Testing* chỉ cho
   *Test users*). Ứng dụng chỉ xin quyền email / hồ sơ cơ bản nên không cần Google xét duyệt.

### Bước 6 — Gửi mã xác nhận Quên mật khẩu qua email (Brevo)

Quên mật khẩu gửi **mã 6 số (hiệu lực 60 giây)** tới email khách. Render gói miễn phí **chặn cổng SMTP**, nên dùng Brevo qua HTTPS:

1. Đăng ký https://www.brevo.com (miễn phí, không cần thẻ).
2. **Senders, domains & dedicated IPs → Senders → Add a sender**: thêm một Gmail làm người gửi, mở thư Brevo gửi tới Gmail đó và bấm link xác minh.
3. **SMTP & API → API keys → Generate a new API key**, copy khóa (chỉ hiện một lần).
4. Trên Render, mỗi service nhập hai biến (`Email__Provider=Brevo` đã có sẵn trong `render.yaml`):
   - `Email__BrevoApiKey` = khóa vừa copy (bí mật).
   - `Email__FromAddress` = Gmail đã xác minh ở bước 2.
5. Lưu để Render deploy lại; log khởi động sẽ ghi `Gửi email qua Brevo (người gửi …)`.

Chưa cấu hình thì mã không được gửi đi (backend ghi lỗi trong log, API vẫn trả thành công để không lộ tài khoản). Khi chạy trên máy phát triển
không đặt `Email:Provider` thì email được lưu thành file `.eml` trong `HornViet.Api/App_Data/mails` — mở file để lấy mã.
Chức năng chỉ dành cho khách hàng: tài khoản Quản trị viên / Nhân viên không nhận mã.

## 4. Biến môi trường

### Render — `hornviet-api-staging` / `hornviet-api`

| Biến | staging | production |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Staging` (có sẵn) | `Production` (có sẵn) |
| `Database__Provider` | `PostgreSql` (có sẵn) | `PostgreSql` (có sẵn) |
| `ConnectionStrings__Default` | chuỗi Neon nhánh `staging` | chuỗi Neon nhánh `main` |
| `Jwt__SecurityKey` | Render tự sinh | Render tự sinh (khác staging) |
| `Seed__AdminPassword`, `Seed__StaffPassword` | mật khẩu mạnh — tài khoản `admin`, `staff` được tạo lần đầu | mật khẩu mạnh, khác staging |
| `Seed__SampleCatalog` | `true` (sản phẩm mẫu) | `false` |
| `App__ClientRootAddress` | URL cửa hàng staging | URL cửa hàng production |
| `App__AdminRootAddress` | URL quản trị staging | URL quản trị production |
| `App__CorsOrigins` | `<cửa hàng staging>,<quản trị staging>` | `<cửa hàng production>,<quản trị production>` |
| `Google__ClientId` | Client ID Google | Client ID Google |
| `Cloudinary__Url` | `cloudinary://...` | `cloudinary://...` |
| `Email__Provider` | `Brevo` (có sẵn) | `Brevo` (có sẵn) |
| `Email__BrevoApiKey` | khóa API Brevo | khóa API Brevo |
| `Email__FromAddress` | Gmail đã xác minh trong Brevo | Gmail đã xác minh trong Brevo |
| `Cloudinary__Folder` | `hornviet-staging` (có sẵn) | `hornviet` (có sẵn) |

Backend **từ chối khởi động** ngoài môi trường Development nếu `Seed__AdminPassword` / `Seed__StaffPassword` còn là
mật khẩu demo — tránh để lộ tài khoản quản trị trên Internet.

### Vercel — `HornViet_customer`

| Biến | Preview (nhánh `staging`) | Production |
|---|---|---|
| `NEXT_PUBLIC_API_BASE_URL` | `https://hornviet-api-staging.onrender.com` | `https://hornviet-api.onrender.com` |
| `NEXT_PUBLIC_ADMIN_URL` | URL quản trị staging | URL quản trị production |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | Client ID Google | Client ID Google |

### Vercel — `HornViet_admin`

| Biến | Preview (nhánh `staging`) | Production |
|---|---|---|
| `NEXT_PUBLIC_API_BASE_URL` | `https://hornviet-api-staging.onrender.com` | `https://hornviet-api.onrender.com` |
| `NEXT_PUBLIC_STORE_URL` | URL cửa hàng staging | URL cửa hàng production |

Biến `NEXT_PUBLIC_*` được gắn vào mã lúc build: đổi giá trị xong phải **Redeploy** mới có hiệu lực.

## 5. Kiểm tra sau khi deploy

- [ ] `https://<backend>/health` trả `Healthy`.
- [ ] Trang cửa hàng tải được danh sách sản phẩm (staging có sản phẩm mẫu).
- [ ] Đăng nhập trang quản trị bằng `admin` / mật khẩu đã đặt ở `Seed__AdminPassword`; chấm "Cập nhật trực tiếp"
      ở thanh bên màu xanh (WebSocket hoạt động).
- [ ] Tải ảnh sản phẩm lên → URL ảnh bắt đầu bằng `https://res.cloudinary.com/`.
- [ ] Khách đặt hàng → trang quản trị hiện thông báo đơn mới ngay.
- [ ] Đăng nhập bằng Google trên trang cửa hàng.
- [ ] Đổi mật khẩu quản trị mặc định sau lần đăng nhập đầu tiên.

## 6. Sự cố thường gặp

| Hiện tượng | Nguyên nhân / cách xử lý |
|---|---|
| Lần mở đầu tiên rất chậm | Render free đang ngủ — chờ ~1 phút |
| Backend log lỗi kết nối CSDL (`Npgsql`, timeout, password authentication failed) | Sai / thiếu `ConnectionStrings__Default`, hoặc copy chuỗi khi đang bật *Connection pooling* |
| Trình duyệt báo lỗi CORS | `App__CorsOrigins` thiếu địa chỉ frontend, hoặc có `/` ở cuối |
| Đăng nhập Google báo `origin_mismatch` | Chưa thêm địa chỉ cửa hàng vào *Authorized JavaScript origins* (chờ vài phút sau khi lưu) |
| Ảnh tải lên bị mất sau vài giờ | Chưa đặt `Cloudinary__Url` — ảnh đang lưu trên ổ đĩa tạm của Render |
| Backend không khởi động, log nhắc `Seed__AdminPassword` | Chưa đặt mật khẩu seed (xem mục 4) |

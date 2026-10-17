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
| Database SQL Server | **Azure SQL Database** (gói miễn phí) | Giữ nguyên SQL Server như khi phát triển — không phải đổi mã nguồn / migration |
| Ảnh sản phẩm, nguyên liệu | **Cloudinary** | Ổ đĩa của Render bị xóa mỗi lần deploy nên ảnh không lưu trên máy chủ |

```
Trình duyệt ──HTTPS──> Vercel (cửa hàng / quản trị)
     │
     └──HTTPS / WSS──> Render (HornViet.Api: REST + Hub /hubs/notifications)
                          ├──TLS 1433──> Azure SQL Database
                          └──HTTPS─────> Cloudinary (lưu ảnh)  ·  Google (kiểm tra đăng nhập Google)
```

Giới hạn của gói miễn phí cần biết:

- **Render free**: tự "ngủ" sau 15 phút không có request; lần truy cập đầu tiên sau đó chờ khoảng 1 phút. Kết nối
  real-time tự nối lại khi server thức dậy.
- **Azure SQL free**: mỗi database 100.000 vCore-giây / tháng, 32 GB; khi rảnh database tự tạm dừng, lần kết nối đầu
  chậm vài giây (EF Core đã bật tự thử lại — `EnableRetryOnFailure`).
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

### Bước 1 — Azure SQL Database

1. Đăng ký **Azure for Students** (https://azure.microsoft.com/free/students) bằng email trường — không cần thẻ.
2. Azure Portal → **Create a resource → SQL Database**:
   - *Resource group*: tạo mới `hornviet`.
   - *Database name*: `hornviet-staging`.
   - *Server*: **Create new** → tên VD `hornviet-sql` (thành `hornviet-sql.database.windows.net`), vùng
     **Southeast Asia**, *Authentication*: **Use SQL authentication**, đặt *Server admin login* và mật khẩu mạnh.
   - Bấm **Apply offer** ở banner *"Want to try Azure SQL Database for free?"* (gói miễn phí, Serverless).
     Chọn *"Auto-pause the database until next month"* khi hết hạn mức (không phát sinh phí).
   - *Networking*: **Public endpoint**, bật *Allow Azure services and resources to access this server*.
3. Tạo database thứ hai `hornviet-prod` trên **cùng server** (cũng **Apply offer**).
4. Vào từng database → **Connection strings → ADO.NET (SQL authentication)**, copy và thay `{your_password}`. Nên tăng
   `Connection Timeout=60` (database tự tạm dừng khi rảnh, lần đầu cần thời gian khởi động lại). Dạng chuỗi:
   ```
   Server=tcp:hornviet-sql.database.windows.net,1433;Initial Catalog=hornviet-prod;Persist Security Info=False;User ID=<admin>;Password=<mật khẩu>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;
   ```
5. Tường lửa: sau khi tạo service trên Render (Bước 3), lấy **Outbound IP ranges** của service (Render → service →
   **Connect → Outbound**) và thêm vào Azure → SQL server → **Networking → Firewall rules**.

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
6. Thêm Outbound IP của **cả hai** service vào tường lửa Azure (Bước 1.5), rồi **Manual Deploy → Deploy latest commit**
   nếu lần deploy đầu bị lỗi kết nối database.

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

### Bước 6 (tùy chọn) — Gửi email Quên mật khẩu

Thêm trên Render (mỗi service): `Smtp__Host=smtp.gmail.com`, `Smtp__UserName=<gmail>`,
`Smtp__Password=<App Password 16 ký tự của Gmail>`. Chưa cấu hình thì link đặt lại mật khẩu không được gửi đi.

## 4. Biến môi trường

### Render — `hornviet-api-staging` / `hornviet-api`

| Biến | staging | production |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Staging` (có sẵn) | `Production` (có sẵn) |
| `ConnectionStrings__Default` | chuỗi kết nối `hornviet-staging` | chuỗi kết nối `hornviet-prod` |
| `Jwt__SecurityKey` | Render tự sinh | Render tự sinh (khác staging) |
| `Seed__AdminPassword`, `Seed__StaffPassword` | mật khẩu mạnh — tài khoản `admin`, `staff` được tạo lần đầu | mật khẩu mạnh, khác staging |
| `Seed__SampleCatalog` | `true` (sản phẩm mẫu) | `false` |
| `App__ClientRootAddress` | URL cửa hàng staging | URL cửa hàng production |
| `App__AdminRootAddress` | URL quản trị staging | URL quản trị production |
| `App__CorsOrigins` | `<cửa hàng staging>,<quản trị staging>` | `<cửa hàng production>,<quản trị production>` |
| `Google__ClientId` | Client ID Google | Client ID Google |
| `Cloudinary__Url` | `cloudinary://...` | `cloudinary://...` |
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
| Backend log lỗi kết nối SQL (error 40615 / timeout) | Chưa thêm Outbound IP của Render vào tường lửa Azure |
| Trình duyệt báo lỗi CORS | `App__CorsOrigins` thiếu địa chỉ frontend, hoặc có `/` ở cuối |
| Đăng nhập Google báo `origin_mismatch` | Chưa thêm địa chỉ cửa hàng vào *Authorized JavaScript origins* (chờ vài phút sau khi lưu) |
| Ảnh tải lên bị mất sau vài giờ | Chưa đặt `Cloudinary__Url` — ảnh đang lưu trên ổ đĩa tạm của Render |
| Backend không khởi động, log nhắc `Seed__AdminPassword` | Chưa đặt mật khẩu seed (xem mục 4) |

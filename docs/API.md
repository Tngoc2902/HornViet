# Hồn Sừng Việt — Tài liệu API cho Frontend

Backend: ASP.NET Core 10 + EF Core + SQL Server, tổ chức N-Layer/DDD theo mẫu ABP.
Swagger (môi trường Development): `https://localhost:44311/swagger`

## 1. Quy ước chung

| Mục | Quy ước |
|---|---|
| Base URL | `https://localhost:44311` (HTTP: `http://localhost:21021`) — đặt vào `NEXT_PUBLIC_API_BASE_URL` của cả hai frontend |
| Frontend | `HornViet_customer` (cửa hàng, `http://localhost:3000`) và `HornViet_admin` (quản trị, `http://localhost:3001`) gọi chung một backend |
| CORS | Backend chỉ nhận origin có trong `App:CorsOrigins` (mặc định `http://localhost:3000,http://localhost:3001`). Origin phải khớp từng ký tự, không có `/` cuối |
| Đường dẫn | `/api/services/app/{Service}/{Action}` (giống ABP), riêng đăng nhập là `/api/TokenAuth/Authenticate` và `/api/TokenAuth/AuthenticateAdmin` |
| Xác thực | Header `Authorization: Bearer <accessToken>` |
| JSON | camelCase; enum gửi/nhận dạng **số**; ngày giờ ISO 8601 theo giờ Việt Nam (`+07:00`) |
| GET / DELETE | Tham số qua query string: `?id=1`, `?categoryIds=1&categoryIds=2` |
| POST / PUT | Body JSON |
| Phân trang | Input `skipCount` (mặc định 0), `maxResultCount` (mặc định 12, tối đa 100). Output `{ totalCount, items }` |
| Ảnh | URL tương đối (`/uploads/...`) — ghép với `NEXT_PUBLIC_API_BASE_URL` khi hiển thị (thẻ `<img>` không cần CORS) |

### 1.1 Vỏ bọc response (mọi API)

```json
// Thành công — dữ liệu nằm trong "result"
{ "result": { ... }, "targetUrl": null, "success": true, "error": null, "unauthorizedRequest": false, "__abp": true }

// Lỗi
{
  "result": null, "success": false, "unauthorizedRequest": false, "__abp": true,
  "error": {
    "code": 0,
    "message": "Thông báo hiển thị cho người dùng",
    "details": "Chi tiết (có thể null, nhiều dòng)",
    "validationErrors": [ { "message": "Email không hợp lệ.", "members": ["emailAddress"] } ]
  }
}
```

| HTTP | Ý nghĩa | Frontend nên làm |
|---|---|---|
| 200 | Thành công | Đọc `result` |
| 400 | Lỗi nghiệp vụ hoặc dữ liệu không hợp lệ | Hiện `error.message`; nếu có `validationErrors` thì gắn lỗi theo từng trường (`members`) |
| 401 | Chưa đăng nhập / token hết hạn / tài khoản bị khóa (`unauthorizedRequest: true`) | Xóa token, chuyển tới trang Đăng nhập |
| 403 | Không đủ quyền | Hiện thông báo, ẩn chức năng |
| 404 | Không tìm thấy (sản phẩm đã ẩn/xóa, đơn không thuộc về mình…) | Hiện `error.message` |
| 409 | Tồn kho vừa bị giao dịch khác thay đổi | Tải lại giỏ hàng, cho khách thử lại |
| 500 | Lỗi hệ thống | Thông báo chung |

### 1.2 Enum

| Enum | Giá trị |
|---|---|
| `OrderStatus` | `0` Chờ xử lý · `1` Đang giao · `2` Hoàn thành · `3` Đã hủy |
| `PaymentMethod` | `0` COD · `1` Chuyển khoản |
| `ProductSorting` | `0` Mới nhất · `1` Giá tăng · `2` Giá giảm · `3` Tên A→Z · `4` Bán chạy |
| `RevenueGroupBy` | `0` Theo ngày · `1` Theo tháng |

### 1.3 Vai trò và quyền

| Vai trò (`roles` trong token) | Quyền |
|---|---|
| `Customer` — Khách hàng | Giỏ hàng, đặt hàng, đơn của tôi, hồ sơ |
| `Staff` — Nhân viên | Như khách + quản lý danh mục, nguyên liệu, sản phẩm/biến thể/tồn kho, đơn hàng, dashboard |
| `Admin` — Quản trị viên | Như nhân viên + quản lý người dùng, thống kê doanh thu & lợi nhuận, nhập kho & giá vốn |

Khách vãng lai (không token) gọi được: đăng ký, đăng nhập, danh mục, nguyên liệu, danh sách và chi tiết sản phẩm.

Tài khoản demo do seed tạo (chỉ môi trường dev, đổi mật khẩu trước khi triển khai): `admin / 123qwe`, `staff / 123qwe`.

---

## 2. Danh sách API

Ký hiệu quyền: 🌐 công khai · 👤 đã đăng nhập · 🛠 Nhân viên + Quản trị viên · 👑 Quản trị viên

### 2.1 Tài khoản (UC Đăng nhập, Đăng ký, Đăng xuất, Quên mật khẩu, Hồ sơ cá nhân)

| Method | Endpoint | Quyền | Body / Query | Result |
|---|---|---|---|---|
| POST | `/api/TokenAuth/Authenticate` | 🌐 | `{ userNameOrEmailAddress, password }` | `{ accessToken, expireInSeconds, userId, userName, name, roles[] }` |
| POST | `/api/TokenAuth/AuthenticateAdmin` | 🌐 | Như `Authenticate` | Như `Authenticate`; tài khoản không có vai trò `Staff`/`Admin` → 400, không cấp token |
| POST | `/api/services/app/Account/Register` | 🌐 | `{ name, emailAddress, phoneNumber, password, userName? }` | `{ userId, userName }` |
| POST | `/api/services/app/Account/ForgotPassword` | 🌐 | `{ emailAddress }` | `null` — luôn thành công (xem bên dưới) |
| POST | `/api/services/app/Account/ResetPassword` | 🌐 | `{ token, newPassword }` — `token` lấy từ link trong email | `null`; mã sai / đã dùng / hết hạn → 400 |
| GET | `/api/services/app/Profile/GetMyProfile` | 👤 | — | `{ id, userName, name, emailAddress, phoneNumber, roleNames[] }` |
| PUT | `/api/services/app/Profile/UpdateMyProfile` | 👤 | `{ name, phoneNumber }` (email không đổi được) | Profile |
| POST | `/api/services/app/Profile/ChangePassword` | 👤 | `{ currentPassword, newPassword }` | `null` |
| GET | `/api/services/app/Profile/GetMyAddresses` | 👤 | — | `{ items: Address[] }` (địa chỉ mặc định đứng đầu) |
| POST | `/api/services/app/Profile/SaveAddress` | 👤 | `{ id?, receiverName, phoneNumber, address, isDefault }` — không có `id` = thêm mới | Address |
| DELETE | `/api/services/app/Profile/DeleteAddress?id=` | 👤 | — | `null` |

- **Đăng xuất**: chỉ cần xóa `accessToken` ở frontend (JWT không lưu phiên phía server). Token hết hạn sau 24 giờ.
- **Thu hồi phiên (SecurityStamp)**: token chứa claim `sstamp`; mỗi request backend đối chiếu với `AbpUsers.SecurityStamp`. Đổi mật khẩu (`Profile/ChangePassword`), đặt lại mật khẩu (`Account/ResetPassword`, `User/ResetPassword`) hoặc đổi vai trò (`User/SetRoles`) tạo dấu mới → mọi token cũ của tài khoản đó nhận **401** ngay, kể cả phiên vừa đổi mật khẩu: frontend đăng xuất và chuyển về trang đăng nhập. Khóa tài khoản cũng cho 401 ngay.
- Mật khẩu: ≥ 6 ký tự, có cả chữ và số. Số điện thoại: `0xxxxxxxxx` hoặc `+84xxxxxxxxx`.
- Cửa hàng (`HornViet_customer`) dùng `Authenticate`; trang quản trị (`HornViet_admin`) dùng `AuthenticateAdmin` để server từ chối tài khoản Khách hàng ngay khi đăng nhập.
- Hai frontend chạy ở hai origin khác nhau nên trình duyệt giữ hai phiên đăng nhập riêng. Quyền vẫn do backend quyết định: token Khách hàng gọi API quản trị nhận 403.

**Quên mật khẩu** (trang `/forgot-password` và `/reset-password` có ở cả hai frontend):

1. `ForgotPassword` luôn trả `success: true`, kể cả khi email chưa đăng ký hoặc tài khoản bị khóa — để không ai dò được email nào đã có tài khoản. Frontend hiển thị: "Nếu email đã đăng ký, chúng tôi đã gửi liên kết…".
2. Email chứa link `{App:ClientRootAddress}/reset-password?token=...` cho Khách hàng, `{App:AdminRootAddress}/reset-password?token=...` cho Nhân viên / Quản trị viên. Tên miền lấy từ cấu hình backend, không lấy từ request.
3. Link có hiệu lực **30 phút**, dùng **một lần**; yêu cầu mới làm link cũ mất hiệu lực. Trong 60 giây sau lần gửi, yêu cầu lặp lại cho cùng tài khoản không gửi thêm email (frontend khóa nút "Gửi lại" 60 giây).
4. CSDL chỉ lưu SHA-256 của mã (bảng `PasswordResetTokens`). Đặt lại xong, mọi link khác của tài khoản cũng mất hiệu lực và mọi phiên đăng nhập đang mở bị đăng xuất (xem SecurityStamp bên dưới).
5. `ResetPassword` kiểm tra độ mạnh mật khẩu trước khi dùng mã, nên mật khẩu yếu (400) không làm mất link.

### 2.2 Danh mục & Nguyên liệu (UC Quản lý danh mục)

| Method | Endpoint | Quyền | Body / Query |
|---|---|---|---|
| GET | `/api/services/app/Category/GetAll` | 🌐 | — → `{ items: [{ id, name, description, productCount, creationTime }] }` |
| GET | `/api/services/app/Category/Get?id=` | 🌐 | |
| POST | `/api/services/app/Category/Create` | 🛠 | `{ name, description? }` |
| PUT | `/api/services/app/Category/Update` | 🛠 | `{ id, name, description? }` |
| DELETE | `/api/services/app/Category/Delete?id=` | 🛠 | Xóa mềm; bị từ chối nếu danh mục còn sản phẩm |
| GET | `/api/services/app/Material/GetAll` | 🌐 | → `{ items: [{ id, name, origin, description }] }` (dùng cho bộ lọc chất liệu) |
| GET / POST / PUT / DELETE | `/api/services/app/Material/Get · Create · Update · Delete` | 🌐 / 🛠 | `{ id?, name, origin?, description? }` |

Tên danh mục / nguyên liệu không được trùng (400 "… đã tồn tại").

### 2.3 Sản phẩm — phía khách (UC Tìm kiếm & lọc, Xem chi tiết)

**GET** `/api/services/app/Product/GetAll` 🌐

| Query | Kiểu | Ghi chú |
|---|---|---|
| `keyword` | string | Tìm theo tên sản phẩm, danh mục, nguyên liệu |
| `categoryIds` | int[] | Lặp tham số: `categoryIds=1&categoryIds=3` |
| `materialIds` | int[] | |
| `minPrice`, `maxPrice` | decimal | |
| `inStockOnly` | bool | |
| `sorting` | `ProductSorting` | Sản phẩm còn hàng luôn được xếp trước |
| `skipCount`, `maxResultCount` | int | |

Result: `{ totalCount, items: [{ id, name, price, mainImage, categoryId, categoryName, materialId, materialName, totalStock, inStock, creationTime }] }`
→ `inStock = false` thì hiện nhãn "Hết hàng".

**GET** `/api/services/app/Product/Get?id=1` 🌐

```json
{
  "id": 1, "name": "Lược sừng trâu chạm khắc", "price": 180000, "mainImage": "/uploads/...", "description": "...",
  "isActive": true, "categoryId": 1, "categoryName": "Lược sừng",
  "material": { "id": 1, "name": "Sừng trâu", "origin": "Làng nghề Thụy Ứng, Hà Nội", "description": "..." },
  "images": ["/uploads/a.jpg", "/uploads/b.jpg"],
  "variants": [
    { "id": 1, "size": "Nhỏ", "style": "Răng thưa", "displayName": "Nhỏ / Răng thưa", "stockQuantity": 12, "inStock": true, "isLowStock": false },
    { "id": 3, "size": "Lớn", "style": "Răng dày", "displayName": "Lớn / Răng dày", "stockQuantity": 0, "inStock": false, "isLowStock": true }
  ],
  "totalStock": 20, "creationTime": "2026-10-05T16:12:10+07:00"
}
```

- Thẻ "Nguồn gốc nguyên liệu" lấy từ `material`.
- Biến thể `inStock = false` → làm mờ, vô hiệu nút "Thêm vào giỏ", hiện "Hết hàng".
- Sản phẩm đã ẩn/xóa → 404 "Sản phẩm không còn tồn tại hoặc đã ngừng kinh doanh." (Nhân viên/Quản trị viên vẫn xem được sản phẩm đang ẩn).

### 2.4 Giỏ hàng (UC Quản lý giỏ hàng) 👤

| Method | Endpoint | Body / Query |
|---|---|---|
| GET | `/api/services/app/Cart/GetMyCart` | — |
| POST | `/api/services/app/Cart/AddItem` | `{ productVariantId, quantity }` — cộng dồn nếu đã có; vượt tồn kho → 400 "chỉ còn N sản phẩm" |
| PUT | `/api/services/app/Cart/UpdateItem` | `{ cartItemId, quantity }` — vượt tồn kho thì tự giới hạn về số còn lại |
| DELETE | `/api/services/app/Cart/DeleteItem?id=` | `id` = cartItemId |

Cả 4 API đều trả về giỏ hiện tại:

```json
{
  "items": [{
    "id": 1, "productVariantId": 1, "productId": 1, "productName": "Lược sừng trâu chạm khắc",
    "mainImage": "/uploads/...", "variantName": "Nhỏ / Răng thưa", "size": "Nhỏ", "style": "Răng thưa",
    "unitPrice": 180000, "quantity": 2, "lineTotal": 360000, "stockQuantity": 12, "isAvailable": true
  }],
  "totalQuantity": 2, "subTotal": 360000, "canCheckout": true
}
```

`isAvailable = false` (ngừng bán hoặc không đủ hàng) → cảnh báo trên dòng đó; `canCheckout = false` → khóa nút Đặt hàng.
Biểu tượng giỏ trên header dùng `totalQuantity`.

### 2.5 Đặt hàng & Đơn hàng của tôi (UC Đặt hàng, Quản lý đơn hàng cá nhân) 👤

| Method | Endpoint | Body / Query | Result |
|---|---|---|---|
| POST | `/api/services/app/Order/Create` | `{ receiverName, phoneNumber, shippingAddress, paymentMethod, note? }` | Order |
| GET | `/api/services/app/Order/GetMyOrders` | `status?`, `skipCount`, `maxResultCount` | `{ totalCount, items: OrderListItem[] }` |
| GET | `/api/services/app/Order/Get?id=` | — | Order (chỉ đơn của mình) |
| POST | `/api/services/app/Order/Cancel` | `{ id }` | Order — chỉ hủy được khi `status = 0`; tồn kho được hoàn lại |

`Order/Create` đặt toàn bộ giỏ hàng hiện tại: kiểm tra tồn kho từng biến thể → tạo đơn (lưu đơn giá lúc mua) → trừ kho → làm trống giỏ, tất cả trong **một giao dịch**. Nếu thiếu hàng: 400, `error.details` liệt kê từng sản phẩm thiếu. Nếu hai khách cùng mua biến thể cuối cùng: một người thành công, người kia nhận 400/409.

```json
// Order
{
  "id": 1, "code": "HSV000001", "creationTime": "...", "status": 0, "paymentMethod": 0,
  "totalAmount": 360000, "itemCount": 2, "canCancel": true, "receiverName": "...",
  "userId": 3, "userName": null, "phoneNumber": "...", "shippingAddress": "...", "note": null,
  "lastStatusChangeTime": null,
  "items": [{ "id": 1, "productVariantId": 1, "productName": "...", "variantName": "Nhỏ / Răng thưa", "quantity": 2, "unitPrice": 180000, "lineTotal": 360000 }]
}
// OrderListItem
{ "id": 1, "code": "HSV000001", "creationTime": "...", "status": 0, "paymentMethod": 0, "totalAmount": 360000, "itemCount": 2, "receiverName": "...", "canCancel": true,
  "items": [{ "productName": "...", "variantName": "Nhỏ / Răng thưa", "quantity": 2 }] }
```

`OrderListItem.items` (dùng cho `GetMyOrders` và `GetAll`) chỉ gồm tên sản phẩm, tên biến thể, số lượng — để danh sách đơn hiện được đơn bán sản phẩm gì mà không phải gọi `Order/Get` từng đơn.

Gợi ý: điền sẵn form thanh toán từ địa chỉ mặc định (`Profile/GetMyAddresses`).

### 2.6 Quản trị sản phẩm & biến thể (UC Quản lý sản phẩm và biến thể) 🛠

| Method | Endpoint | Body / Query |
|---|---|---|
| GET | `/api/services/app/Product/GetAdminList` | `keyword?, categoryId?, isActive?, lowStockOnly?, skipCount, maxResultCount` → `{ id, name, mainImage, price, categoryName, materialName, variantCount, totalStock, isLowStock, isActive, creationTime }` |
| GET | `/api/services/app/Product/Get?id=` | Dữ liệu cho form sửa |
| POST | `/api/services/app/Product/Create` | ProductInput (không có `id`) |
| PUT | `/api/services/app/Product/Update` | ProductInput (có `id`) |
| PUT | `/api/services/app/Product/SetActive` | `{ id, isActive }` — ẩn / hiện trên cửa hàng |
| DELETE | `/api/services/app/Product/Delete?id=` | Xóa mềm; lịch sử đơn vẫn giữ nguyên |
| POST | `/api/services/app/File/UploadImage` | `multipart/form-data`, trường `file` (jpg/png/webp, ≤ 5 MB) → `{ url }` |

```json
// ProductInput
{
  "id": 1, "categoryId": 1, "materialId": 1, "name": "Lược sừng trâu chạm khắc", "price": 180000,
  "mainImage": "/uploads/202610/abc.jpg", "description": "...", "isActive": true,
  "variants": [
    { "id": 1, "size": "Nhỏ", "style": "Răng thưa", "stockQuantity": 12 },
    { "size": "Lớn", "style": "Răng dày", "stockQuantity": 5 }
  ],
  "images": ["/uploads/202610/a.jpg", "/uploads/202610/b.jpg"]
}
```

- Bắt buộc ít nhất 1 biến thể; không được trùng cặp (size, style).
- Khi **Update**: biến thể có `id` được sửa (kể cả tồn kho), không có `id` là thêm mới, biến thể cũ không gửi lên sẽ bị xóa mềm (và gỡ khỏi giỏ của khách).
- `images` là danh sách đầy đủ theo thứ tự hiển thị (ghi đè danh sách cũ).
- Quy trình form: upload từng ảnh qua `File/UploadImage` → lấy `url` → gửi vào `mainImage` / `images`.
- Nhãn "Sắp hết" khi tồn kho biến thể ≤ 5 (`isLowStock`).

### 2.7 Quản trị đơn hàng (UC Quản lý đơn hàng) 🛠

| Method | Endpoint | Body / Query |
|---|---|---|
| GET | `/api/services/app/Order/GetAll` | `status?, keyword?` (mã đơn HSV…, tên người nhận, SĐT), `fromDate?, toDate?` (yyyy-MM-dd), `skipCount, maxResultCount` |
| GET | `/api/services/app/Order/Get?id=` | Chi tiết đơn bất kỳ (có thêm `userName`) |
| PUT | `/api/services/app/Order/UpdateStatus` | `{ id, status }` |

Chuyển trạng thái hợp lệ: `0 → 1 → 2`, hủy (`3`) từ `0` hoặc `1` (tự hoàn tồn kho). Lùi trạng thái hay sửa đơn đã Hoàn thành/Đã hủy → 400.
Nút hành động gợi ý: Chờ xử lý → [Giao hàng] [Hủy]; Đang giao → [Hoàn thành] [Hủy].

### 2.8 Quản lý người dùng 👑

| Method | Endpoint | Body / Query |
|---|---|---|
| GET | `/api/services/app/User/GetAll` | `keyword?, roleName?, isActive?, skipCount, maxResultCount` → `{ id, userName, name, emailAddress, phoneNumber, isActive, creationTime, roleNames[] }` |
| GET | `/api/services/app/User/Get?id=` | |
| GET | `/api/services/app/User/GetRoles` | `{ items: [{ id, name, displayName }] }` |
| POST | `/api/services/app/User/Create` | `{ name, emailAddress, userName?, phoneNumber?, password, roleNames: ["Staff"] }` — tạo tài khoản nhân viên |
| PUT | `/api/services/app/User/SetRoles` | `{ userId, roleNames: ["Staff"] }` |
| PUT | `/api/services/app/User/SetActive` | `{ userId, isActive }` — khóa / mở khóa |
| PUT | `/api/services/app/User/ResetPassword` | `{ userId, newPassword }` → `null` — đặt lại mật khẩu cho tài khoản khác |

Không thể tự khóa hoặc tự gỡ quyền Admin của chính mình. Tài khoản bị khóa mất phiên ngay ở request kế tiếp (401).
`ResetPassword` không áp dụng cho chính mình (dùng `Profile/ChangePassword`); mật khẩu mới theo cùng quy tắc ≥ 6 ký tự, có cả chữ và số.

### 2.9 Thống kê & Dashboard

| Method | Endpoint | Quyền | Query → Result |
|---|---|---|---|
| GET | `/api/services/app/Statistic/GetDashboard` | 🛠 | `{ pendingOrderCount, shippingOrderCount, todayOrderCount, customerCount, todayRevenue, monthRevenue, lowStockVariants: [{ productId, productName, variantId, variantName, stockQuantity }] }` — thêm `monthProfit`; `todayRevenue`/`monthRevenue`/`monthProfit` là `null` với Nhân viên |
| GET | `/api/services/app/Statistic/GetRevenue` | 👑 | `fromDate?, toDate?` (mặc định 30 ngày gần nhất), `groupBy` → `{ fromDate, toDate, totalRevenue, totalCost, totalProfit, profitMargin, uncostedQuantity, orderCount, items: [{ period, revenue, orderCount, cost, profit, uncostedQuantity }] }` — `profitMargin` (%) là `null` khi doanh thu = 0 |
| GET | `/api/services/app/Statistic/GetTopProducts` | 👑 | `fromDate?, toDate?, top = 10` → `{ items: [{ productId, productName, quantitySold, revenue, profit, uncostedQuantity }] }` |

Doanh thu chỉ tính đơn **Hoàn thành**, theo ngày đặt hàng. `items` chỉ có các ngày/tháng phát sinh doanh thu — frontend tự điền 0 cho kỳ trống khi vẽ biểu đồ.
Giá vốn = Σ `số lượng × giá vốn lưu trên dòng đơn lúc bán`; lợi nhuận gộp = doanh thu − giá vốn. Dòng đơn bán khi biến thể chưa có giá vốn (trước phiếu nhập đầu tiên) không được tính vào giá vốn/lợi nhuận và được đếm ở `uncostedQuantity` để frontend cảnh báo.

### 2.10 Nhập kho & giá vốn 👑

| Method | Endpoint | Body / Query → Result |
|---|---|---|
| GET | `/api/services/app/StockReceipt/GetVariantCosts` | `keyword?, skipCount, maxResultCount` → `{ productVariantId, productId, productName, variantName, salePrice, stockQuantity, averageCost, stockValue, unitProfit, lastReceiptTime }` — `averageCost`/`stockValue`/`unitProfit` là `null` khi chưa có phiếu nhập |
| GET | `/api/services/app/StockReceipt/GetAll` | `productVariantId?, keyword?, skipCount, maxResultCount` → `{ id, creationTime, productId, productName, productVariantId, variantName, quantity, unitCost, totalCost, note, creatorUserName, lastModificationTime, canEdit }` — mới nhất trước |
| POST | `/api/services/app/StockReceipt/Create` | `{ productVariantId, quantity, unitCost, note? }` — cộng tồn kho và tính lại giá vốn bình quân |
| PUT | `/api/services/app/StockReceipt/Update` | `{ id, quantity, unitCost, note? }` — sửa phiếu nhập gõ nhầm |
| DELETE | `/api/services/app/StockReceipt/Delete?id=` | → `null` — hủy phiếu nhập |

Giá vốn tính theo **bình quân gia quyền di động** cho từng biến thể: `giá vốn mới = (tồn × giá vốn cũ + SL nhập × giá nhập) / (tồn + SL nhập)`. Hàng tồn từ trước khi có chức năng này (chưa có giá vốn) lấy giá của phiếu nhập đầu tiên. Bán hàng, hủy đơn, sửa tồn kho thủ công không làm đổi giá vốn.
Mỗi dòng đơn lưu giá vốn tại thời điểm đặt hàng, nên giá nhập về sau không làm sai lợi nhuận đơn cũ. Giá vốn không bao giờ trả về cho khách hoặc Nhân viên.
Chỉ **phiếu mới nhất** của một biến thể được sửa/xóa (`canEdit`), phiếu cũ hơn trả 400. Khi sửa/xóa, giá vốn bình quân được tính lại từ trạng thái trước phiếu và giá vốn của các đơn đặt sau phiếu đó được cập nhật theo. Không thể giảm số lượng / xóa phiếu nếu hàng của phiếu đã bán ra (400).

---

## 3. HTTP client ở frontend

Cả hai repo (Next.js) dùng chung một client `lib/api/http.ts` (fetch, không dùng axios) và `lib/api/services.ts`:

| Mục | Cách làm |
|---|---|
| Base URL | `NEXT_PUBLIC_API_BASE_URL` trong `.env.local` (dev: `http://localhost:21021`) |
| Token | Lưu ở `localStorage`, gửi `Authorization: Bearer <token>`. Hai origin 3000 / 3001 có `localStorage` riêng nên hai phiên độc lập |
| Response | Trả thẳng `result`; lỗi chuẩn hóa thành `ApiError { status, message, details, validationErrors }`, `fieldErrors()` gom lỗi theo `members` |
| 401 | Xóa token, quay về `/login` |
| Mảng trong query | Lặp tên: `categoryIds=1&categoryIds=2` |

Khác biệt duy nhất giữa hai repo trong `services.ts`: trang quản trị đăng nhập qua `/api/TokenAuth/AuthenticateAdmin`
và có thêm `userApi.resetPassword`. Khi backend đổi API, cập nhật `lib/api` ở cả hai repo.

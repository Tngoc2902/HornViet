# Giải thích luồng xử lý của 18 use case (đối chiếu mã nguồn)

Tài liệu giải thích từng use case của hệ thống Hồn Sừng Việt theo **đúng mã nguồn hiện tại**: API nào được gọi, đi qua lớp nào, kiểm tra gì, ghi gì xuống cơ sở dữ liệu và trả về gì. Mỗi use case gồm:

- **API**: phương thức HTTP và đường dẫn (tiền tố chung `/api/services/app/` trừ hai API đăng nhập).
- **Mã nguồn**: vị trí `tệp:dòng` của controller, application service và phương thức tầng miền.
- **Luồng chính**: các bước theo thứ tự thực thi, kèm trích đoạn mã quan trọng.
- **Luồng thay thế**: điều kiện lỗi, nơi kiểm tra và thông báo/mã HTTP trả về (ghi đúng chuỗi trong code).
- **Ghi nhớ khi bảo vệ**: ý chính nên trình bày với hội đồng.

---

## 0. Đường đi chung của một request

Mọi use case đều đi qua cùng một "đường ống" sau:

```
Giao diện Next.js (fetch + header Authorization: Bearer <token>)
  → ExceptionHandlingMiddleware (lưới an toàn bắt lỗi chưa xử lý → 500)
  → Xác thực JWT: kiểm tra chữ ký, hạn, rồi OnTokenValidated đối chiếu IsActive + SecurityStamp
  → Phân quyền: [Authorize] / [Authorize(Policy = Management | AdminOnly)]
  → Controller (kế thừa HornVietBaseController) → Run(() => _service.XxxAsync(input))
  → Application Service (HornViet.Application) → thực thể tầng miền (HornViet.Domain)
  → IRepository.GetAll()/InsertAsync()/Delete() + IUnitOfWork.SaveChangesAsync() (một giao dịch)
  → WrapResultFilter bọc kết quả thành AjaxResponse { success, result, error, unAuthorizedRequest }
```

**Kiểm tra token ở mọi request** — [Authentication.cs:120](../HornViet.Api/Authorization/Authentication.cs#L120):

```csharp
OnTokenValidated = async ctx =>
{
    var userIdValue = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
    var stamp = ctx.Principal?.FindFirstValue(TokenService.SecurityStampClaim);
    var users = ctx.HttpContext.RequestServices.GetRequiredService<IRepository<User, long>>();
    var active = long.TryParse(userIdValue, out var userId) && stamp != null &&
                 await users.GetAll().AnyAsync(u => u.Id == userId && u.IsActive && u.SecurityStamp == stamp);
    if (!active)
        ctx.Fail("Tài khoản không còn hoạt động.");
},
```

→ Tài khoản bị khóa, hoặc `SecurityStamp` đã đổi (đổi/đặt lại mật khẩu, đổi vai trò) thì token cũ bị từ chối ngay: **401** "Bạn chưa đăng nhập hoặc phiên đăng nhập đã hết hạn.". Không đủ quyền theo policy: **403** "Bạn không có quyền thực hiện chức năng này.".

**Hai chính sách phân quyền** — [Authentication.cs:142](../HornViet.Api/Authorization/Authentication.cs#L142):

```csharp
.AddPolicy(AuthorizationPolicies.Management, p => p.RequireRole(nameof(RoleName.Admin), nameof(RoleName.Staff)))
.AddPolicy(AuthorizationPolicies.AdminOnly, p => p.RequireRole(nameof(RoleName.Admin)));
```

**Chuyển lỗi nghiệp vụ thành mã HTTP** — [HornVietBaseController.cs:21](../HornViet.Api/Controllers/HornVietBaseController.cs#L21) gọi [ResponseHandling.cs:38](../HornViet.Api/Middlewares/ResponseHandling.cs#L38):

| Ngoại lệ ném ra trong service/entity | Mã HTTP | Ý nghĩa |
|---|---|---|
| `EntityNotFoundException` | 404 | Không tìm thấy dữ liệu |
| `UserFriendlyException` | 400 | Vi phạm quy tắc nghiệp vụ, `error.message` hiển thị được cho người dùng |
| `DbUpdateConcurrencyException` | 409 | "Dữ liệu (tồn kho) vừa được cập nhật bởi giao dịch khác. Vui lòng tải lại và thử lại." |
| Lỗi kiểm tra DTO (`[Required]`, `[Range]`…) | 400 | "Dữ liệu không hợp lệ." kèm `validationErrors` |
| Lỗi khác | 500 | Middleware ghi log, trả thông báo chung |

**Lưu dữ liệu**: `HornVietDbContext` cài đặt `IUnitOfWork`; trước khi lưu, `ApplyAuditConcepts` ([HornVietDbContext.cs:228](../HornViet.Infrastructure/Persistence/HornVietDbContext.cs#L228)) tự gán `CreationTime` và đổi lệnh xóa thành xóa mềm (`IsDeleted = true`); bộ lọc toàn cục ([HornVietDbContext.cs:210](../HornViet.Infrastructure/Persistence/HornVietDbContext.cs#L210)) tự loại bản ghi đã xóa mềm khỏi mọi truy vấn.

---

## A. Nhóm chung cho mọi người dùng

### UC1. Đăng nhập

- **Tác nhân**: Khách hàng, Nhân viên, Quản trị viên.
- **API**: `POST /api/TokenAuth/Authenticate` (trang cửa hàng) · `POST /api/TokenAuth/AuthenticateAdmin` (trang quản trị).
- **Mã nguồn**: [TokenAuthController.cs:25](../HornViet.Api/Controllers/Account/TokenAuthController.cs#L25), [AccountAppService.cs:206](../HornViet.Application/Accounts/AccountAppService.cs#L206) `ValidateCredentialsAsync`, [Authentication.cs:45](../HornViet.Api/Authorization/Authentication.cs#L45) `TokenService.CreateToken`.

**Luồng chính**

1. Người dùng nhập tên đăng nhập hoặc email và mật khẩu; giao diện gửi `{ userNameOrEmailAddress, password }`.
2. Service tìm tài khoản theo **tên đăng nhập hoặc email** (email so ở dạng chữ thường), nạp kèm vai trò:

   ```csharp
   var login = input.UserNameOrEmailAddress.Trim();
   var user = await _userRepository.GetAll()
       .Include(u => u.Roles).ThenInclude(r => r.Role)
       .FirstOrDefaultAsync(u => u.UserName == login || u.EmailAddress == login.ToLower());
   // Cùng một thông báo cho sai tên đăng nhập và sai mật khẩu để không lộ tài khoản nào tồn tại.
   if (user == null ||
       _passwordHasher.VerifyHashedPassword(user, user.Password, input.Password) == PasswordVerificationResult.Failed)
       throw new UserFriendlyException("Tên đăng nhập hoặc mật khẩu không chính xác.");
   if (!user.IsActive)
       throw new UserFriendlyException("Tài khoản của bạn đã bị khóa. Vui lòng liên hệ cửa hàng.");
   ```

3. Với trang quản trị, controller kiểm tra thêm vai trò:

   ```csharp
   var user = await _accountAppService.ValidateCredentialsAsync(model);
   if (!user.Roles.Contains(nameof(RoleName.Admin)) && !user.Roles.Contains(nameof(RoleName.Staff)))
       throw new UserFriendlyException("Tài khoản không có quyền truy cập trang quản trị.");
   return CreateResult(user);
   ```

4. `TokenService.CreateToken` phát hành JWT ký **HMAC-SHA256**, hạn `Jwt:ExpirationHours` (mặc định 24 giờ), chứa mã người dùng, tên, từng vai trò và claim `sstamp` = `SecurityStamp`.
5. Kết quả `{ accessToken, expireInSeconds, userId, userName, name, roles }`; giao diện lưu token vào `localStorage` và chuyển về trang trước đó (`returnUrl`) hoặc Trang chủ / trang Tổng quan.

**Luồng thay thế**

| Điều kiện | Nơi kiểm tra | Kết quả |
|---|---|---|
| Bỏ trống trường | DTO `[Required]` | 400 "Dữ liệu không hợp lệ." |
| Sai tên đăng nhập **hoặc** sai mật khẩu | `ValidateCredentialsAsync` | 400 "Tên đăng nhập hoặc mật khẩu không chính xác." |
| Đúng mật khẩu nhưng tài khoản bị khóa | `ValidateCredentialsAsync` | 400 "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ cửa hàng." |
| Khách hàng đăng nhập trang quản trị | `AuthenticateAdmin` | 400 "Tài khoản không có quyền truy cập trang quản trị.", không cấp token |

**Ghi nhớ khi bảo vệ**: mật khẩu chỉ lưu chuỗi băm PBKDF2 (`PasswordHasher<User>`); kiểm tra trạng thái khóa **sau** khi đúng mật khẩu để không lộ thông tin; token có `SecurityStamp` nên thu hồi được trước hạn (mục 0).

---

### UC2. Đăng xuất

- **Tác nhân**: mọi người dùng · **API**: không gọi API.

**Luồng chính**: giao diện xóa token và thông tin phiên trong `localStorage`, chuyển về Trang chủ (cửa hàng) hoặc trang Đăng nhập (quản trị).

**Luồng thay thế**: bất kỳ API nào trả **401** (token hết hạn, tài khoản bị khóa, `SecurityStamp` đã đổi) → giao diện tự xóa phiên như đăng xuất.

**Ghi nhớ khi bảo vệ**: đăng xuất chỉ phía trình duyệt; token đã phát về lý thuyết vẫn hợp lệ đến khi hết hạn nếu bị sao chép. Hướng cải tiến: gọi API đổi `SecurityStamp` khi đăng xuất hoặc dùng refresh token ngắn hạn.

---

### UC3. Quên mật khẩu và đặt lại mật khẩu

- **API**: `POST Account/ForgotPassword { emailAddress }` → `POST Account/ResetPassword { token, newPassword }`.
- **Mã nguồn**: [AccountAppService.cs:225](../HornViet.Application/Accounts/AccountAppService.cs#L225) `ForgotPasswordAsync`, [AccountAppService.cs:276](../HornViet.Application/Accounts/AccountAppService.cs#L276) `ResetPasswordAsync`, [PasswordResetToken.cs:37](../HornViet.Domain/Users/Entities/PasswordResetToken.cs#L37).

**Luồng chính – gửi liên kết**

1. Tìm tài khoản theo email; **không có hoặc bị khóa thì dừng lặng lẽ** nhưng vẫn trả thành công:

   ```csharp
   // Email chưa đăng ký hoặc tài khoản bị khóa: không gửi gì nhưng vẫn trả thành công như bình thường.
   if (user == null || !user.IsActive)
       return;
   ```

2. Chống spam: đã gửi trong 60 giây gần nhất (`AppConsts.PasswordResetResendSeconds`) thì không gửi thêm.
3. Thu hồi các mã cũ còn hiệu lực rồi phát mã mới — chỉ lưu **giá trị băm SHA-256**, hạn 30 phút:

   ```csharp
   foreach (var old in pending)
       old.Revoke(now);
   var (token, rawToken) = PasswordResetToken.Issue(
       user.Id, now, TimeSpan.FromMinutes(AppConsts.PasswordResetTokenLifetimeMinutes));
   ```

4. Tạo liên kết `{AdminRootAddress | ClientRootAddress}/reset-password?token=<mã gốc>` — Nhân viên/QTV nhận liên kết về trang quản trị, Khách hàng về trang cửa hàng — và gửi qua `IEmailSender` (SMTP; nếu `Smtp:Host` trống thì ghi tệp `.eml` vào `App_Data/mails`).
5. Gửi lỗi → ghi log, thu hồi mã vừa tạo, **không** báo lỗi ra ngoài (tránh lộ email tồn tại).

**Luồng chính – đặt mật khẩu mới**

```csharp
var hash = PasswordResetToken.Hash(input.Token);
var token = await _resetTokenRepository.GetAll().FirstOrDefaultAsync(t => t.TokenHash == hash);
if (token == null || !token.IsUsable(now))                 // chưa dùng và chưa hết hạn
    throw new UserFriendlyException(PasswordResetToken.InvalidMessage);
// ... tài khoản phải còn hoạt động
PasswordPolicy.Validate(input.NewPassword);                // kiểm tra trước khi dùng mã
user.Password = _passwordHasher.HashPassword(user, input.NewPassword);
user.RotateSecurityStamp();                                // mọi phiên đăng nhập cũ mất hiệu lực
token.MarkUsed(now);
// thu hồi các mã khác của tài khoản rồi SaveChangesAsync
```

**Luồng thay thế**

| Điều kiện | Kết quả |
|---|---|
| Email không tồn tại / tài khoản khóa | Vẫn trả thành công, không gửi email |
| Gửi lại trong 60 giây | Không gửi thêm |
| Mã sai, đã dùng, hết hạn | 400 "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới." |
| Mật khẩu mới yếu | 400 "Mật khẩu phải có ít nhất 6 ký tự, gồm cả chữ và số." — mã **vẫn dùng được** để nhập lại |

**Ghi nhớ khi bảo vệ**: ba lớp bảo vệ — không lộ email tồn tại, mã dùng một lần có hạn, CSDL chỉ lưu băm nên lộ CSDL cũng không dùng được liên kết. Có 7 ca unit test cho `PasswordResetToken`.

---

## B. Nhóm Khách hàng

### UC4. Đăng ký

- **API**: `POST Account/Register { name, emailAddress, phoneNumber, password, userName? }`.
- **Mã nguồn**: [AccountAppService.cs:176](../HornViet.Application/Accounts/AccountAppService.cs#L176) `RegisterAsync`, [AccountAppService.cs:320](../HornViet.Application/Accounts/AccountAppService.cs#L320) `PasswordPolicy`.

**Luồng chính**

```csharp
PasswordPolicy.Validate(input.Password);                        // ≥ 6 ký tự, có cả chữ và số
var email = input.EmailAddress.Trim().ToLowerInvariant();
var userName = string.IsNullOrWhiteSpace(input.UserName) ? email : input.UserName.Trim();
if (await _userRepository.GetAll().AnyAsync(u => u.EmailAddress == email || u.UserName == userName))
    throw new UserFriendlyException("Tài khoản này đã được sử dụng.");
// Tài khoản tự đăng ký luôn chỉ có vai trò Khách hàng.
var customerRole = await _roleRepository.GetAll().SingleAsync(r => r.Name == nameof(RoleName.Customer));
// ... tạo User, băm mật khẩu, gắn UserRole(Customer), InsertAsync + SaveChangesAsync
```

Giao diện nhận `{ userId, userName }` rồi chuyển tới `/login?email=…` (email điền sẵn).

**Luồng thay thế**

| Điều kiện | Nơi kiểm tra | Kết quả |
|---|---|---|
| Thiếu trường, email sai định dạng, số điện thoại không khớp `^(0\|\+84)(3\|5\|7\|8\|9)\d{8}$` | DTO | 400 "Dữ liệu không hợp lệ." |
| Mật khẩu yếu | `PasswordPolicy` | 400 "Mật khẩu phải có ít nhất 6 ký tự, gồm cả chữ và số." |
| Trùng email hoặc tên đăng nhập | `RegisterAsync` | 400 "Tài khoản này đã được sử dụng." |

**Ghi nhớ khi bảo vệ**: không cho người dùng tự chọn vai trò; tài khoản Nhân viên chỉ do Quản trị viên tạo (UC16).

---

### UC5. Quản lý hồ sơ cá nhân

- **API**: `GET Profile/GetMyProfile` · `PUT UpdateMyProfile { name, phoneNumber }` · `POST ChangePassword` · `GET GetMyAddresses` · `POST SaveAddress` · `DELETE DeleteAddress?id=` — controller gắn `[Authorize]`.
- **Mã nguồn**: [ProfileAppService.cs:96](../HornViet.Application/Profiles/ProfileAppService.cs#L96), [:106](../HornViet.Application/Profiles/ProfileAppService.cs#L106), [:133](../HornViet.Application/Profiles/ProfileAppService.cs#L133).

**Luồng chính**

- **Cập nhật hồ sơ**: chỉ sửa họ tên và số điện thoại; DTO không có email nên email không đổi được.
- **Đổi mật khẩu**:

  ```csharp
  if (_passwordHasher.VerifyHashedPassword(user, user.Password, input.CurrentPassword) == PasswordVerificationResult.Failed)
      throw new UserFriendlyException("Mật khẩu hiện tại không đúng.");
  PasswordPolicy.Validate(input.NewPassword);
  user.Password = _passwordHasher.HashPassword(user, input.NewPassword);
  user.RotateSecurityStamp(); // đăng xuất mọi phiên (kể cả phiên hiện tại) — frontend yêu cầu đăng nhập lại
  ```

- **Sổ địa chỉ**: không có `id` thì thêm mới, có `id` thì sửa (chỉ địa chỉ của chính mình); luôn giữ **đúng một** địa chỉ mặc định:

  ```csharp
  // Luôn có đúng một địa chỉ mặc định.
  if (input.IsDefault || myAddresses.All(a => !a.IsDefault || a == address))
  {
      foreach (var a in myAddresses) a.IsDefault = false;
      address.IsDefault = true;
  }
  ```

  Danh sách trả về sắp địa chỉ mặc định lên đầu; trang thanh toán chọn sẵn địa chỉ này.

**Luồng thay thế**: sai mật khẩu hiện tại → 400 "Mật khẩu hiện tại không đúng."; sửa/xóa địa chỉ không phải của mình → 404 "Không tìm thấy địa chỉ.".

---

### UC6. Tìm kiếm và lọc sản phẩm (không cần đăng nhập)

- **API**: `GET Product/GetAll?keyword&categoryIds&materialIds&minPrice&maxPrice&sorting&skipCount&maxResultCount`.
- **Mã nguồn**: [ProductAppService.cs:60](../HornViet.Application/Catalog/ProductAppService.cs#L60).

**Luồng chính**

```csharp
var query = _productRepository.GetAll().Where(p => p.IsActive);           // chỉ sản phẩm đang bán (+ lọc xóa mềm)
if (!string.IsNullOrWhiteSpace(input.Keyword))
    query = query.Where(p => p.Name.Contains(keyword) || p.Category.Name.Contains(keyword) ||
                             p.Material.Name.Contains(keyword));
if (input.CategoryIds is { Count: > 0 }) query = query.Where(p => input.CategoryIds.Contains(p.CategoryId));
// ... MaterialIds, MinPrice, MaxPrice
// Quy tắc nghiệp vụ: sản phẩm còn hàng luôn được ưu tiên hiển thị trước.
var ordered = query.OrderByDescending(p => p.Variants.Any(v => v.StockQuantity > 0));
ordered = input.Sorting switch
{
    ProductSorting.PriceAsc => ordered.ThenBy(p => p.Price),
    ProductSorting.PriceDesc => ordered.ThenByDescending(p => p.Price),
    ProductSorting.NameAsc => ordered.ThenBy(p => p.Name),
    ProductSorting.BestSelling => /* tổng số lượng trong các đơn Completed */,
    _ => ordered.ThenByDescending(p => p.CreationTime)                      // mặc định: mới nhất
};
```

Kết quả phân trang (`PageBy`, mặc định 12/trang) gồm `totalCount` và các dòng có `inStock`, `totalStock`.

**Vì sao gõ không dấu vẫn tìm được?** CSDL dùng collation `Latin1_General_100_CI_AI` ([HornVietDbContext.cs:40](../HornViet.Infrastructure/Persistence/HornVietDbContext.cs#L40)) — so sánh không phân biệt hoa thường và dấu, nên `Contains("luoc sung")` khớp "Lược sừng".

**Luồng thay thế**: không có kết quả → `totalCount = 0`, giao diện hiện thông báo không tìm thấy.

---

### UC7. Xem chi tiết sản phẩm (không cần đăng nhập)

- **API**: `GET Product/Get?id=` · **Mã nguồn**: [ProductAppService.cs:119](../HornViet.Application/Catalog/ProductAppService.cs#L119).

**Luồng chính**

```csharp
var product = await _productRepository.GetAll().AsNoTracking()
    .Include(p => p.Category).Include(p => p.Material)
    .Include(p => p.Variants).Include(p => p.Images)
    .FirstOrDefaultAsync(p => p.Id == id);
if (product == null || (!product.IsActive && !_session.IsManagement()))
    throw new EntityNotFoundException(NotAvailableMessage);
return ToDetailDto(product);   // ảnh, nguyên liệu (nguồn gốc), biến thể + stockQuantity, inStock, isLowStock
```

Trên giao diện, khi chọn biến thể, trang hiện số còn lại và số đã có trong giỏ; biến thể `inStock = false` bị làm mờ, nút hiện "Hết hàng".

**Luồng thay thế**

| Điều kiện | Kết quả |
|---|---|
| Sản phẩm bị xóa mềm hoặc đang ẩn (khách) | 404 "Sản phẩm không còn tồn tại hoặc đã ngừng kinh doanh." |
| Nhân viên/QTV xem sản phẩm đang ẩn | Vẫn xem được (`IsManagement()`) |
| Khách chưa đăng nhập bấm mua | Nút "Đăng nhập để mua" → `/login?returnUrl=/products/{id}` |

---

### UC8. Quản lý giỏ hàng (cần đăng nhập)

- **API**: `GET Cart/GetMyCart` · `POST AddItem { productVariantId, quantity }` · `PUT UpdateItem { cartItemId, quantity }` · `DELETE DeleteItem?id=` — controller gắn `[Authorize]`.
- **Mã nguồn**: [CartAppService.cs:84](../HornViet.Application/Carts/CartAppService.cs#L84), [Cart.cs:14](../HornViet.Domain/Carts/Entities/Cart.cs#L14) `AddOrIncrease`, [Cart.cs:35](../HornViet.Domain/Carts/Entities/Cart.cs#L35) `EnsureInStock`.

**Luồng chính – thêm vào giỏ**

```csharp
var variant = await _variantRepository.GetAll().Include(v => v.Product)
                  .FirstOrDefaultAsync(v => v.Id == input.ProductVariantId)
              ?? throw new EntityNotFoundException("Biến thể sản phẩm không tồn tại.");
if (!variant.Product.IsActive)
    throw new UserFriendlyException("Sản phẩm đã ngừng kinh doanh.");
var cart = await LoadCartAsync();                 // giỏ của người dùng hiện tại (IAppSession)
if (cart.Id == 0)
    await _cartRepository.InsertAsync(cart);      // chưa có giỏ → tạo mới
cart.AddOrIncrease(variant, input.Quantity);      // quy tắc nghiệp vụ nằm trong thực thể Cart
await _unitOfWork.SaveChangesAsync();
```

Trong tầng miền:

```csharp
public CartItem AddOrIncrease(ProductVariant variant, int quantity)
{
    if (quantity <= 0) throw new UserFriendlyException("Số lượng phải lớn hơn 0.");
    var item = Items.FirstOrDefault(i => i.ProductVariantId == variant.Id);
    var newQuantity = (item?.Quantity ?? 0) + quantity;   // cộng dồn với số đã có trong giỏ
    EnsureInStock(variant, newQuantity);
    // ... thêm dòng mới hoặc cập nhật số lượng
}
public static void EnsureInStock(ProductVariant variant, int quantity)
{
    if (variant.StockQuantity <= 0)
        throw new UserFriendlyException($"Biến thể \"{variant.DisplayName}\" đã hết hàng.");
    if (quantity > variant.StockQuantity)
        throw new UserFriendlyException($"Biến thể \"{variant.DisplayName}\" chỉ còn {variant.StockQuantity} sản phẩm.");
}
```

**Sửa số lượng**: vượt tồn thì **tự giới hạn** về số còn lại — `item.Quantity = Math.Min(input.Quantity, stock);` ([CartAppService.cs:103](../HornViet.Application/Carts/CartAppService.cs#L103)).

**Kết quả** `CartDto`: các dòng (đơn giá hiện tại, thành tiền, `IsAvailable` = còn bán và đủ hàng), `SubTotal`, `CanCheckout`.

**Luồng thay thế**

| Điều kiện | Kết quả |
|---|---|
| Biến thể không tồn tại | 404 "Biến thể sản phẩm không tồn tại." |
| Sản phẩm đang ẩn | 400 "Sản phẩm đã ngừng kinh doanh." |
| Tổng số lượng vượt tồn | 400 "Biến thể "…" chỉ còn N sản phẩm." |
| Biến thể hết hàng (thêm) | 400 "Biến thể "…" đã hết hàng." |
| Sửa dòng có biến thể đã hết | 400 "Biến thể "…" đã hết hàng, vui lòng xóa khỏi giỏ." |
| Sửa/xóa dòng không thuộc giỏ | 404 "Sản phẩm không có trong giỏ hàng." |

**Ghi nhớ khi bảo vệ**: mỗi người một giỏ (chỉ mục duy nhất `Carts.UserId`), mỗi biến thể một dòng (duy nhất `CartId + ProductVariantId`). Thêm vào giỏ **chưa** trừ tồn kho — tồn chỉ bị trừ khi đặt hàng (UC9). 6 ca unit test cho `Cart`.

---

### UC9. Đặt hàng — use case quan trọng nhất

- **API**: `POST Order/Create { receiverName, phoneNumber, shippingAddress, paymentMethod, note? }`.
- **Mã nguồn**: [OrderAppService.cs:158](../HornViet.Application/Orders/OrderAppService.cs#L158) `CreateAsync`, [Catalog.cs:145](../HornViet.Domain/Catalog/Entities/Catalog.cs#L145) `DecreaseStock`, [Order.cs:40](../HornViet.Domain/Orders/Entities/Order.cs#L40) `AddItem`.

**Luồng chính**

```csharp
var userId = _session.GetUserId();
var cartItems = await _cartItemRepository.GetAll()
    .Include(ci => ci.ProductVariant).ThenInclude(v => v.Product)
    .Where(ci => _cartRepository.GetAll().Any(c => c.Id == ci.CartId && c.UserId == userId))
    .OrderBy(ci => ci.Id).ToListAsync();
if (cartItems.Count == 0)
    throw new UserFriendlyException("Giỏ hàng đang trống.");

// (1) Gom mọi lỗi tồn kho để khách sửa giỏ một lần.
var problems = cartItems
    .Where(ci => !ci.ProductVariant.Product.IsActive || ci.Quantity > ci.ProductVariant.StockQuantity)
    .Select(ci => /* "… đã ngừng kinh doanh." hoặc "… chỉ còn N sản phẩm." */).ToList();
if (problems.Count > 0)
    throw new UserFriendlyException("Một số sản phẩm trong giỏ không đủ hàng, vui lòng cập nhật lại giỏ.",
        string.Join("\n", problems));

// (2) Tạo đơn ở trạng thái Pending, mỗi dòng: trừ tồn + chụp thông tin lúc mua + xóa khỏi giỏ.
var order = new Order(userId, input.ReceiverName.Trim(), input.PhoneNumber.Trim(),
    input.ShippingAddress.Trim(), input.PaymentMethod, input.Note?.Trim());
foreach (var item in cartItems)
{
    var variant = item.ProductVariant;
    variant.DecreaseStock(item.Quantity);
    order.AddItem(variant.Id, variant.Product.Name, variant.DisplayName, item.Quantity, variant.Product.Price,
        variant.AverageCost); // giá vốn lúc bán → tính lợi nhuận
    _cartItemRepository.Delete(item);
}
await _orderRepository.InsertAsync(order);
await _unitOfWork.SaveChangesAsync();   // (3) một giao dịch: UPDATE tồn kho, INSERT đơn + dòng đơn, DELETE dòng giỏ
return ToDto(order, null);              // mã đơn HSV000123 (Order.FormatCode)
```

`Order.AddItem` tính lại tổng tiền: `TotalAmount = Items.Sum(i => i.UnitPrice * i.Quantity);`. `DecreaseStock` là chốt chặn cuối ở tầng miền (số lượng phải > 0 và không vượt tồn).

**Chống bán vượt tồn khi đặt đồng thời**: cột `RowVersion` của `ProductVariants` được cấu hình `IsRowVersion()` ([HornVietDbContext.cs:79](../HornViet.Infrastructure/Persistence/HornVietDbContext.cs#L79)). Hai khách cùng đọc tồn kho = 1 và cùng đặt: giao dịch lưu sau có `RowVersion` cũ → `UPDATE … WHERE RowVersion = @cũ` không khớp dòng nào → EF ném `DbUpdateConcurrencyException` → **409**, toàn bộ giao dịch không được lưu.

**Luồng thay thế**

| Điều kiện | Kết quả |
|---|---|
| Thiếu thông tin giao hàng, số điện thoại sai | 400 "Dữ liệu không hợp lệ." |
| Giỏ trống | 400 "Giỏ hàng đang trống." |
| Có dòng thiếu hàng / ngừng bán | 400 "Một số sản phẩm trong giỏ không đủ hàng, vui lòng cập nhật lại giỏ." + `error.details` liệt kê từng dòng |
| Xung đột cập nhật đồng thời | 409 "Dữ liệu (tồn kho) vừa được cập nhật bởi giao dịch khác. Vui lòng tải lại và thử lại." |

**Ghi nhớ khi bảo vệ**: ba ý — (1) tất cả trong một lần `SaveChangesAsync` = một giao dịch; (2) `RowVersion` chống bán vượt tồn; (3) dòng đơn chụp tên, đơn giá, giá vốn nên lịch sử không đổi khi sản phẩm bị sửa/xóa, và lợi nhuận tính đúng. Chuyển khoản chỉ được ghi nhận (`PaymentMethod.BankTransfer`), chưa có cổng thanh toán.

---

### UC10. Quản lý đơn hàng cá nhân

- **API**: `GET Order/GetMyOrders?status&skipCount&maxResultCount` · `GET Order/Get?id=` · `POST Order/Cancel { id }`.
- **Mã nguồn**: [OrderAppService.cs:198](../HornViet.Application/Orders/OrderAppService.cs#L198), [:208](../HornViet.Application/Orders/OrderAppService.cs#L208), [:224](../HornViet.Application/Orders/OrderAppService.cs#L224), [:273](../HornViet.Application/Orders/OrderAppService.cs#L273) `RestockAsync`.

**Luồng chính**

- **Danh sách**: lọc `o.UserId == userId`, có thể lọc theo trạng thái, phân trang.
- **Chi tiết**: khách chỉ xem được đơn của mình; Nhân viên/QTV xem mọi đơn (kèm `userName`).
- **Hủy đơn**:

  ```csharp
  var order = await LoadOrderAsync(id);
  if (order.UserId != _session.GetUserId())
      throw new EntityNotFoundException("Không tìm thấy đơn hàng.");
  if (order.Status != OrderStatus.Pending)
      throw new UserFriendlyException("Đơn hàng đã được xử lý hoặc đang giao, không thể hủy.");
  order.ChangeStatus(OrderStatus.Canceled);
  await RestockAsync(order);
  await _unitOfWork.SaveChangesAsync();
  ```

  `RestockAsync` cộng lại tồn kho, **bỏ qua bộ lọc xóa mềm** để biến thể đã ngừng bán vẫn được hoàn đúng sổ kho:

  ```csharp
  var variants = await _variantRepository.GetAll().IgnoreQueryFilters()
      .Where(v => variantIds.Contains(v.Id)).ToListAsync();
  foreach (var item in order.Items)
      variants.First(v => v.Id == item.ProductVariantId).IncreaseStock(item.Quantity);
  ```

**Luồng thay thế**: đơn của người khác → 404 "Không tìm thấy đơn hàng." (trả 404 thay vì 403 để không lộ đơn tồn tại); đơn không còn Chờ xử lý → 400 "Đơn hàng đã được xử lý hoặc đang giao, không thể hủy.".

---

## C. Nhóm Nhân viên (Quản trị viên kế thừa toàn bộ)

### UC11. Xem trang tổng quan

- **API**: `GET Statistic/GetDashboard` — controller gắn `[Authorize(Policy = Management)]`.
- **Mã nguồn**: [StatisticAppService.cs:204](../HornViet.Application/Statistics/StatisticAppService.cs#L204).

**Luồng chính**

```csharp
PendingOrderCount = await orders.CountAsync(o => o.Status == OrderStatus.Pending),
ShippingOrderCount = await orders.CountAsync(o => o.Status == OrderStatus.Shipping),
TodayOrderCount = await orders.CountAsync(o => o.CreationTime >= today),
CustomerCount = await _userRepository.GetAll().CountAsync(u => u.Roles.Any(r => r.Role.Name == nameof(RoleName.Customer))),
LowStockVariants = /* tồn ≤ AppConsts.LowStockThreshold (5), sản phẩm đang bán, tối đa 10 biến thể */
...
if (_session.IsInRole(RoleName.Admin))
{
    dto.TodayRevenue = /* tổng TotalAmount đơn Completed hôm nay */;
    dto.MonthRevenue = /* tổng đơn Completed trong tháng */;
    dto.MonthProfit  = /* Σ (UnitPrice − UnitCost) × Quantity của dòng có giá vốn */;
}
```

Giao diện gọi thêm `Order/GetAll` để hiện danh sách đơn mới nhất.

**Ghi nhớ khi bảo vệ**: Nhân viên **không nhận** số liệu doanh thu/lợi nhuận từ server (giá trị `null`), chứ không chỉ ẩn trên giao diện.

---

### UC12. Quản lý sản phẩm và biến thể

- **API**: `POST File/UploadImage` · `GET Product/GetAdminList` · `POST Product/Create` · `PUT Product/Update` · `PUT Product/SetActive` · `DELETE Product/Delete` — chính sách Management.
- **Mã nguồn**: [FileController.cs:24](../HornViet.Api/Controllers/Admin/FileController.cs#L24), [ProductAppService.cs:174](../HornViet.Application/Catalog/ProductAppService.cs#L174) Create, [:189](../HornViet.Application/Catalog/ProductAppService.cs#L189) Update, [:243](../HornViet.Application/Catalog/ProductAppService.cs#L243) SetActive, [:252](../HornViet.Application/Catalog/ProductAppService.cs#L252) Delete, [:267](../HornViet.Application/Catalog/ProductAppService.cs#L267) ValidateAsync.

**Luồng chính**

1. **Tải ảnh trước**: chỉ nhận `.jpg/.jpeg/.png/.webp`, content type `image/*`, tối đa 5 MB; lưu vào `wwwroot/uploads/yyyyMM/<guid>.<đuôi>` và trả `{ url }`.
2. **Kiểm tra quy tắc** (dùng chung cho thêm và sửa):

   ```csharp
   if (input.Variants.Count == 0)
       throw new UserFriendlyException("Sản phẩm phải có ít nhất một biến thể.");
   var duplicate = input.Variants
       .GroupBy(v => (Clean(v.Size)?.ToLower(), Clean(v.Style)?.ToLower()))
       .FirstOrDefault(g => g.Count() > 1);
   if (duplicate != null)
       throw new UserFriendlyException("Có hai biến thể trùng kích thước và kiểu dáng.");
   if (!await _categoryRepository.GetAll().AnyAsync(c => c.Id == input.CategoryId))
       throw new UserFriendlyException("Danh mục không tồn tại.");
   if (!await _materialRepository.GetAll().AnyAsync(m => m.Id == input.MaterialId))
       throw new UserFriendlyException("Nguyên liệu không tồn tại.");
   ```

3. **Thêm**: tạo `Product` kèm `new ProductVariant(size, style, stock)` (hàm tạo gọi `SetStock`, không cho âm) và danh sách ảnh.
4. **Sửa** — đồng bộ biến thể:

   ```csharp
   // Đồng bộ biến thể: sửa biến thể có Id, thêm biến thể mới, xóa mềm biến thể bị bỏ đi.
   var removed = product.Variants.Where(pv => !keepIds.Contains(pv.Id)).ToList();
   foreach (var variant in removed)
       variant.IsDeleted = true;
   // Biến thể đã ngừng bán thì không thể còn nằm trong giỏ hàng của khách → xóa các dòng giỏ liên quan.
   foreach (var v in input.Variants)
   {
       if (v.Id is { } variantId) { /* cập nhật Size, Style, SetStock(v.StockQuantity) */ }
       else product.Variants.Add(new ProductVariant(Clean(v.Size), Clean(v.Style), v.StockQuantity));
   }
   ```

5. **Ẩn/hiện**: `product.IsActive = input.IsActive` — sản phẩm ẩn vẫn quản lý được, khách không thấy và không mua được.
6. **Xóa**: xóa các dòng giỏ chứa biến thể của sản phẩm rồi `_productRepository.Delete(product)` → `ApplyAuditConcepts` đổi thành **xóa mềm**.

**Luồng thay thế**: ảnh quá 5 MB hoặc sai định dạng → 400; không có biến thể / trùng biến thể / danh mục, nguyên liệu không tồn tại → 400 (các thông báo trên); `SetStock` âm → 400 "Tồn kho không được âm."; biến thể gửi lên không thuộc sản phẩm → 400 "Biến thể #… không thuộc sản phẩm này.".

**Ghi nhớ khi bảo vệ**: phân biệt **ẩn** (tạm ngừng bán, bật lại được) và **xóa mềm** (biến mất khỏi mọi truy vấn nhưng dữ liệu còn, đơn cũ vẫn tham chiếu được). Giá đặt ở cấp sản phẩm nên mọi biến thể chung giá.

---

### UC13. Quản lý danh mục

- **API**: `GET Category/GetAll, Get` (công khai) · `POST Create` · `PUT Update` · `DELETE Delete` (Management).
- **Mã nguồn**: [CategoryMaterialAppServices.cs:63](../HornViet.Application/Catalog/CategoryMaterialAppServices.cs#L63) Create, [:85](../HornViet.Application/Catalog/CategoryMaterialAppServices.cs#L85) Delete, [:95](../HornViet.Application/Catalog/CategoryMaterialAppServices.cs#L95) EnsureUniqueNameAsync.

**Luồng chính**: thêm/sửa tên, mô tả (kiểm tra tên duy nhất); danh sách kèm số sản phẩm đang bán (`productCount`). Xóa:

```csharp
if (await _productRepository.GetAll().AnyAsync(p => p.CategoryId == id))
    throw new UserFriendlyException("Danh mục đang chứa sản phẩm. Hãy chuyển hoặc ẩn các sản phẩm trước khi xóa.");
_categoryRepository.Delete(category);   // → xóa mềm
```

**Luồng thay thế**: tên trùng → 400 'Danh mục "…" đã tồn tại. Vui lòng nhập tên khác.'; còn sản phẩm → 400 như trên. Ở tầng CSDL còn có chỉ mục duy nhất có điều kiện `Name WHERE IsDeleted = 0` làm chốt chặn cuối.

---

### UC14. Quản lý nguyên liệu

- **API**: `Material/GetAll, Get` (công khai) · `Create, Update, Delete` (Management).
- **Mã nguồn**: [CategoryMaterialAppServices.cs:175](../HornViet.Application/Catalog/CategoryMaterialAppServices.cs#L175), [:200](../HornViet.Application/Catalog/CategoryMaterialAppServices.cs#L200).

**Luồng chính**: như danh mục, thêm trường nguồn gốc (`Origin`, VD làng nghề Thụy Ứng) và ảnh minh họa (`Image`, đường dẫn từ `File/UploadImage`; chuỗi rỗng được chuẩn hóa thành `null`). Ảnh hiển thị ở mục "Dòng hàng nổi bật" của trang chủ và trang Nguồn gốc; thẻ "Nguồn gốc nguyên liệu" ở trang chi tiết sản phẩm lấy từ đây.

**Luồng thay thế**: tên trùng → 400 'Nguyên liệu "…" đã tồn tại.'; đang được sản phẩm dùng → 400 "Nguyên liệu đang được dùng cho sản phẩm, không thể xóa.".

---

### UC15. Quản lý đơn hàng

- **API**: `GET Order/GetAll?status&keyword&fromDate&toDate` · `GET Order/Get?id=` · `PUT Order/UpdateStatus { id, status }` — chính sách Management.
- **Mã nguồn**: [OrderAppService.cs:240](../HornViet.Application/Orders/OrderAppService.cs#L240), [:261](../HornViet.Application/Orders/OrderAppService.cs#L261), [Order.cs:59](../HornViet.Domain/Orders/Entities/Order.cs#L59) `ChangeStatus`.

**Luồng chính**

1. **Tìm đơn**: từ khóa được thử đổi thành mã đơn (`TryParseOrderCode`: bỏ tiền tố "HSV", lấy số) để so với `Id`, đồng thời tìm theo tên người nhận, số điện thoại; lọc trạng thái và khoảng ngày (ngày kết thúc tính hết ngày).
2. **Đổi trạng thái**:

   ```csharp
   var order = await LoadOrderAsync(input.Id);
   order.ChangeStatus(input.Status);
   if (input.Status == OrderStatus.Canceled)
       await RestockAsync(order);
   await _unitOfWork.SaveChangesAsync();
   ```

3. **Máy trạng thái trong tầng miền**:

   ```csharp
   var allowed = (Status, newStatus) switch
   {
       (OrderStatus.Pending, OrderStatus.Shipping) => true,
       (OrderStatus.Shipping, OrderStatus.Completed) => true,
       (OrderStatus.Pending, OrderStatus.Canceled) => true,
       (OrderStatus.Shipping, OrderStatus.Canceled) => true,
       _ => false
   };
   if (!allowed)
       throw new UserFriendlyException($"Không thể chuyển đơn {FormatCode(Id)} từ trạng thái {Status} sang {newStatus}.");
   Status = newStatus;
   LastStatusChangeTime = DateTime.Now;
   ```

**Luồng thay thế**: đơn không tồn tại → 404 "Không tìm thấy đơn hàng."; chuyển sai thứ tự, lùi trạng thái hoặc đổi đơn đã Hoàn thành/Đã hủy → 400 "Không thể chuyển đơn HSV… từ trạng thái … sang …".

**Ghi nhớ khi bảo vệ**: quy tắc chuyển trạng thái đặt trong thực thể `Order`, không phải ở controller hay giao diện — gọi từ đâu cũng không vượt qua được; có 11 ca unit test cho `Order`. Khác với khách (UC10 chỉ hủy khi Chờ xử lý), nhân viên hủy được cả đơn Đang giao.

---

## D. Nhóm chỉ dành cho Quản trị viên

### UC16. Quản lý người dùng

- **API**: `User/GetAll, Get, GetRoles, Create, SetRoles, SetActive, ResetPassword` — controller gắn `[Authorize(Policy = AdminOnly)]`.
- **Mã nguồn**: [UserAppService.cs:161](../HornViet.Application/Users/UserAppService.cs#L161) Create, [:187](../HornViet.Application/Users/UserAppService.cs#L187) SetRoles, [:214](../HornViet.Application/Users/UserAppService.cs#L214) SetActive, [:226](../HornViet.Application/Users/UserAppService.cs#L226) ResetPassword.

**Luồng chính**

- **Tạo tài khoản**: giống đăng ký (chính sách mật khẩu, email chữ thường, kiểm tra trùng) nhưng gán **vai trò do QTV chọn** (thường là `Staff`).
- **Đổi vai trò**:

  ```csharp
  if (user.Id == _session.GetUserId() && roles.All(r => r.Name != nameof(RoleName.Admin)))
      throw new UserFriendlyException("Bạn không thể tự gỡ quyền Quản trị viên của chính mình.");
  // ... xóa vai trò bị bỏ, thêm vai trò mới
  // Token cũ còn mang vai trò cũ: thu hồi để người dùng đăng nhập lại và nhận quyền mới.
  if (removed.Count > 0 || added.Count > 0)
      user.RotateSecurityStamp();
  ```

- **Khóa / mở khóa**: `user.IsActive = input.IsActive` — token của tài khoản bị khóa bị chặn ngay ở request kế tiếp (`OnTokenValidated` kiểm tra `IsActive`).
- **Đặt lại mật khẩu cho người khác**: kiểm tra chính sách, băm mật khẩu mới, `RotateSecurityStamp()`.

**Luồng thay thế**

| Điều kiện | Kết quả |
|---|---|
| Tự khóa mình | 400 "Bạn không thể khóa tài khoản của chính mình." |
| Tự gỡ vai trò Admin của mình | 400 "Bạn không thể tự gỡ quyền Quản trị viên của chính mình." |
| Đặt lại mật khẩu cho chính mình | 400 "Hãy dùng chức năng Đổi mật khẩu để đổi mật khẩu của chính bạn." |
| Email/tên đăng nhập trùng | 400 "Tài khoản này đã được sử dụng." |
| Nhân viên gọi API này | 403 "Bạn không có quyền thực hiện chức năng này." |

**Ghi nhớ khi bảo vệ**: các ràng buộc "không tự …" tránh Quản trị viên tự khóa mình khỏi hệ thống; mọi thay đổi quyền có hiệu lực ngay nhờ `SecurityStamp`.

---

### UC17. Thống kê báo cáo

- **API**: `GET Statistic/GetRevenue?fromDate&toDate&groupBy` · `GET Statistic/GetTopProducts?fromDate&toDate&top` — AdminOnly.
- **Mã nguồn**: [StatisticAppService.cs:117](../HornViet.Application/Statistics/StatisticAppService.cs#L117), [:173](../HornViet.Application/Statistics/StatisticAppService.cs#L173), [:244](../HornViet.Application/Statistics/StatisticAppService.cs#L244) `CompletedOrders`.

**Luồng chính**

1. Xác định khoảng thời gian: mặc định 30 ngày gần nhất; ngày kết thúc tính hết ngày.
2. Chỉ lấy đơn **Hoàn thành** theo ngày đặt:

   ```csharp
   private IQueryable<Order> CompletedOrders(DateTime from, DateTime toExclusive) =>
       _orderRepository.GetAll().Where(o =>
           o.Status == OrderStatus.Completed && o.CreationTime >= from && o.CreationTime < toExclusive);
   ```

3. Doanh thu, số đơn gom theo ngày hoặc tháng (`GroupBy` trên `CreationTime`).
4. Giá vốn và lợi nhuận tính từ **dòng đơn** (giá vốn đã chụp lúc bán):

   ```csharp
   point.Cost   = periodLines.Where(l => l.UnitCost != null).Sum(l => l.UnitCost!.Value * l.Quantity);
   point.Profit = periodLines.Where(l => l.UnitCost != null).Sum(l => (l.UnitPrice - l.UnitCost!.Value) * l.Quantity);
   point.UncostedQuantity = periodLines.Where(l => l.UnitCost == null).Sum(l => l.Quantity);
   ...
   ProfitMargin = costedRevenue > 0 ? Math.Round(totalProfit / costedRevenue * 100, 1) : null,
   ```

5. `GetTopProducts`: xếp sản phẩm theo tổng số lượng bán trong các đơn Hoàn thành (mặc định top 10), kèm doanh thu và lợi nhuận.

**Luồng thay thế**: ngày bắt đầu sau ngày kết thúc → 400 "Ngày bắt đầu phải trước ngày kết thúc."; không có đơn → danh sách rỗng, giao diện hiện "Chưa có dữ liệu".

**Ghi nhớ khi bảo vệ**: tỷ suất lợi nhuận chỉ tính trên phần doanh thu **có giá vốn**; số lượng bán chưa có giá vốn (đơn trước khi dùng chức năng nhập kho) báo riêng ở `UncostedQuantity` để không làm sai số liệu.

---

### UC18. Nhập kho và giá vốn

- **API**: `GET StockReceipt/GetVariantCosts` · `GET StockReceipt/GetAll` · `POST Create` · `PUT Update` · `DELETE Delete?id=` — AdminOnly.
- **Mã nguồn**: [StockReceiptAppService.cs:184](../HornViet.Application/Inventory/StockReceiptAppService.cs#L184) Create, [:201](../HornViet.Application/Inventory/StockReceiptAppService.cs#L201) Update, [:211](../HornViet.Application/Inventory/StockReceiptAppService.cs#L211) Delete, [StockReceipt.cs:40](../HornViet.Domain/Inventory/Entities/StockReceipt.cs#L40) `Receive`, [Catalog.cs:177](../HornViet.Domain/Catalog/Entities/Catalog.cs#L177) `MovingAverage`, [Catalog.cs:185](../HornViet.Domain/Catalog/Entities/Catalog.cs#L185) `ReceiveStock`, [Catalog.cs:199](../HornViet.Domain/Catalog/Entities/Catalog.cs#L199) `ReplaceLatestReceipt`.

**Luồng chính – lập phiếu nhập**

1. `CreateAsync` nạp biến thể (404 nếu không có), gọi phương thức tạo phiếu của tầng miền rồi lưu một lần:

   ```csharp
   var receipt = StockReceipt.Receive(variant, input.Quantity, input.UnitCost, Clean(input.Note), _session.GetUserId());
   await _receiptRepository.InsertAsync(receipt);
   await _unitOfWork.SaveChangesAsync();
   ```

2. `StockReceipt.Receive` ghi lại **trạng thái trước khi nhập** rồi mới cộng kho:

   ```csharp
   Validate(quantity, unitCost);                       // 1 – 1.000.000; 0 – 999.999.999
   var receipt = new StockReceipt
   {
       ProductVariantId = variant.Id, Quantity = quantity, UnitCost = unitCost, Note = note,
       StockBefore = variant.StockQuantity,            // để sau này sửa/hủy tính lại chính xác
       AverageCostBefore = variant.AverageCost,
       CreatorUserId = creatorUserId
   };
   variant.ReceiveStock(quantity, unitCost);
   ```

3. `ReceiveStock` tính **giá vốn bình quân gia quyền di động**:

   ```csharp
   public static decimal MovingAverage(int stockBefore, decimal? averageBefore, int quantityIn, decimal unitCostIn)
   {
       if (stockBefore <= 0 || averageBefore is null)
           return unitCostIn;                           // chưa có tồn / chưa có giá vốn → lấy giá nhập
       return Math.Round((stockBefore * averageBefore.Value + quantityIn * unitCostIn) / (stockBefore + quantityIn), 4);
   }
   ```

   Ví dụ: tồn 10 cái giá vốn 100.000đ, nhập thêm 5 cái giá 130.000đ → (10×100.000 + 5×130.000) / 15 = **110.000đ**, tồn mới 15.

**Luồng chính – sửa / hủy phiếu**

1. `LoadLatestAsync` chỉ cho thao tác với **phiếu mới nhất** của biến thể:

   ```csharp
   if (await _receiptRepository.GetAll().AnyAsync(r => r.ProductVariantId == receipt.ProductVariantId && r.Id > receipt.Id))
       throw new UserFriendlyException(NotLatestMessage);
   ```

2. `Correct` (sửa) hoặc `Revert` (hủy, tương đương số lượng mới = 0) gọi `ReplaceLatestReceipt`, tính lại từ `StockBefore`, `AverageCostBefore`:

   ```csharp
   var stockAfter = StockQuantity + (newQuantity - oldQuantity);
   if (stockAfter < 0)
       throw new UserFriendlyException(
           $"Không thể giảm số lượng phiếu nhập: hàng đã bán, biến thể \"{DisplayName}\" chỉ còn {StockQuantity} sản phẩm.");
   StockQuantity = stockAfter;
   AverageCost = newQuantity == 0 ? averageBefore : MovingAverage(stockBefore, averageBefore, newQuantity, newUnitCost);
   ```

3. `RecostSalesAfterAsync` gán lại giá vốn cho các dòng đơn đã bán biến thể này **sau thời điểm lập phiếu** → báo cáo lợi nhuận tự đúng:

   ```csharp
   foreach (var line in lines)          // dòng đơn của biến thể, đơn tạo từ receipt.CreationTime trở đi
       line.UnitCost = receipt.ProductVariant.AverageCost;
   ```

**Luồng thay thế**

| Điều kiện | Nơi kiểm tra | Kết quả |
|---|---|---|
| Chưa chọn biến thể | DTO `[Range]` | 400 "Vui lòng chọn biến thể." |
| Số lượng ngoài 1 – 1.000.000 | DTO + `StockReceipt.Validate` | 400 "Số lượng nhập phải từ 1 đến 1.000.000." |
| Giá nhập âm hoặc > 999.999.999 | DTO + `StockReceipt.Validate` | 400 "Giá nhập không hợp lệ." |
| Biến thể không tồn tại | `CreateAsync` | 404 "Biến thể sản phẩm không tồn tại." |
| Sửa/hủy phiếu không phải mới nhất | `LoadLatestAsync` | 400 "Chỉ sửa / hủy được phiếu nhập mới nhất của biến thể này (đã có phiếu nhập sau phiếu này)." |
| Giảm/hủy làm tồn kho âm (hàng đã bán) | `ReplaceLatestReceipt` | 400 "Không thể giảm số lượng phiếu nhập: hàng đã bán, …" |

**Ghi nhớ khi bảo vệ**: chỉ phiếu nhập làm thay đổi giá vốn; bán hàng, hủy đơn, kiểm kê (`SetStock`) dùng giá vốn hiện tại. Lưu `StockBefore`/`AverageCostBefore` trong phiếu là "ảnh chụp" giúp sửa/hủy phiếu mới nhất mà không phải tính lại toàn bộ lịch sử. Có 13 ca unit test cho phần này (`StockReceiptTests`).

---

## Phụ: bảng tra nhanh use case → API → lớp xử lý

| # | Use case | API chính | Application Service | Quy tắc ở tầng miền |
|---|---|---|---|---|
| 1 | Đăng nhập | `TokenAuth/Authenticate(Admin)` | `AccountAppService.ValidateCredentialsAsync` | — (TokenService ở tầng Api) |
| 2 | Đăng xuất | (không có) | — | — |
| 3 | Quên mật khẩu | `Account/ForgotPassword`, `ResetPassword` | `AccountAppService` | `PasswordResetToken.Issue/IsUsable/MarkUsed/Revoke`, `User.RotateSecurityStamp` |
| 4 | Đăng ký | `Account/Register` | `AccountAppService.RegisterAsync` | — |
| 5 | Hồ sơ cá nhân | `Profile/*` | `ProfileAppService` | `User.RotateSecurityStamp` |
| 6 | Tìm kiếm & lọc | `Product/GetAll` | `ProductAppService.GetAllAsync` | — |
| 7 | Xem chi tiết | `Product/Get` | `ProductAppService.GetAsync` | `ProductVariant.DisplayName` |
| 8 | Giỏ hàng | `Cart/*` | `CartAppService` | `Cart.AddOrIncrease`, `Cart.EnsureInStock` |
| 9 | Đặt hàng | `Order/Create` | `OrderAppService.CreateAsync` | `ProductVariant.DecreaseStock`, `Order.AddItem` |
| 10 | Đơn cá nhân | `Order/GetMyOrders, Get, Cancel` | `OrderAppService` | `Order.ChangeStatus`, `ProductVariant.IncreaseStock` |
| 11 | Tổng quan | `Statistic/GetDashboard` | `StatisticAppService.GetDashboardAsync` | — |
| 12 | Sản phẩm & biến thể | `File/UploadImage`, `Product/*` | `ProductAppService` | `ProductVariant.SetStock` |
| 13 | Danh mục | `Category/*` | `CategoryAppService` | — |
| 14 | Nguyên liệu | `Material/*` | `MaterialAppService` | — |
| 15 | Đơn hàng | `Order/GetAll, UpdateStatus` | `OrderAppService` | `Order.ChangeStatus` |
| 16 | Người dùng | `User/*` | `UserAppService` | `User.RotateSecurityStamp` |
| 17 | Thống kê | `Statistic/GetRevenue, GetTopProducts` | `StatisticAppService` | — |
| 18 | Nhập kho & giá vốn | `StockReceipt/*` | `StockReceiptAppService` | `StockReceipt.Receive/Correct/Revert`, `ProductVariant.ReceiveStock/ReplaceLatestReceipt/MovingAverage` |

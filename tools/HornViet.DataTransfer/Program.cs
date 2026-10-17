// Chuyển dữ liệu một lần từ SQL Server (máy phát triển) lên PostgreSQL (staging / production trên Neon).
//
//   dotnet run --project tools/HornViet.DataTransfer                 → CHẠY THỬ: chỉ đếm số dòng, không ghi gì
//   dotnet run --project tools/HornViet.DataTransfer -- --execute    → ghi thật (một giao dịch, lỗi thì hoàn tác)
//
// Biến môi trường:
//   TARGET_CONNECTION  (bắt buộc)  chuỗi kết nối PostgreSQL đích — dạng URI postgresql://... của Neon dán nguyên được
//   SOURCE_CONNECTION  (tùy chọn)  mặc định: SQL Server localhost, database HonSungVietDb
//   CLOUDINARY_URL     (tùy chọn)  cloudinary://KEY:SECRET@CLOUD — có thì ảnh /uploads/... được tải lên Cloudinary
//   CLOUDINARY_FOLDER  (tùy chọn)  thư mục trên Cloudinary, mặc định "hornviet"
//   UPLOADS_DIR        (tùy chọn)  thư mục chứa ảnh trên máy, mặc định HornViet.Api/wwwroot
//
// An toàn: từ chối chạy nếu CSDL đích đã có sản phẩm hoặc đơn hàng; không chép tài khoản đã tồn tại ở đích (admin / staff
// đã seed) mà ánh xạ sang tài khoản đó; không chép mã đặt lại mật khẩu; chỉ in SỐ LƯỢNG, không in nội dung dữ liệu.
using System.Reflection;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using HornViet.Domain.Catalog.Entities;
using HornViet.Domain.Users.Entities;
using HornViet.Infrastructure.Persistence;
using HornViet.Infrastructure.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

var execute = args.Contains("--execute");
var targetConn = Environment.GetEnvironmentVariable("TARGET_CONNECTION");
if (string.IsNullOrWhiteSpace(targetConn))
    return Fail("Thiếu biến môi trường TARGET_CONNECTION (chuỗi kết nối PostgreSQL đích).");

var sourceConn = Environment.GetEnvironmentVariable("SOURCE_CONNECTION")
                 ?? "Server=localhost; Database=HonSungVietDb; Trusted_Connection=True; TrustServerCertificate=True;";
var uploadsRoot = Environment.GetEnvironmentVariable("UPLOADS_DIR") ?? FindUploadsRoot();
var cloudinaryUrl = Environment.GetEnvironmentVariable("CLOUDINARY_URL");
var cloudinaryFolder = Environment.GetEnvironmentVariable("CLOUDINARY_FOLDER") ?? "hornviet";

await using var source = new HornVietDbContext(new DbContextOptionsBuilder<HornVietDbContext>().UseSqlServer(sourceConn).Options);
await using var target = new PostgreSqlHornVietDbContext(new DbContextOptionsBuilder<PostgreSqlHornVietDbContext>()
    .UseNpgsql(PostgreSqlConnectionString.Normalize(targetConn)).Options);

Console.WriteLine(execute ? "CHẾ ĐỘ GHI THẬT (--execute)" : "CHẾ ĐỘ CHẠY THỬ (không ghi gì; thêm --execute để ghi thật)");

// 1. Kiểm tra CSDL đích
if (!await target.Database.CanConnectAsync()) return Fail("Không kết nối được CSDL đích.");
var pending = (await target.Database.GetPendingMigrationsAsync()).ToList();
if (pending.Count > 0)
    return Fail($"CSDL đích còn {pending.Count} migration chưa áp. Hãy để backend khởi động (Database:MigrateOnStartup=true) trước.");
if (await target.Products.IgnoreQueryFilters().AnyAsync() || await target.Orders.IgnoreQueryFilters().AnyAsync())
    return Fail("CSDL đích đã có sản phẩm hoặc đơn hàng — không chuyển để tránh ghi đè / trùng dữ liệu.");

// 2. Ánh xạ tài khoản & vai trò đã có ở đích (admin / staff đã seed): local Id → Id ở đích
var targetUsers = await target.Users.IgnoreQueryFilters().AsNoTracking().ToListAsync();
var targetRoles = await target.Roles.IgnoreQueryFilters().AsNoTracking().ToListAsync();
var userMap = new Dictionary<long, long>();
var roleMap = new Dictionary<int, int>();
foreach (var u in await source.Users.IgnoreQueryFilters().AsNoTracking().ToListAsync())
    if (targetUsers.FirstOrDefault(t => t.UserName == u.UserName) is { } t) userMap[u.Id] = t.Id;
foreach (var r in await source.Roles.IgnoreQueryFilters().AsNoTracking().ToListAsync())
    if (targetRoles.FirstOrDefault(t => t.Name == r.Name) is { } t) roleMap[r.Id] = t.Id;
var usedUserIds = targetUsers.Select(u => u.Id).ToHashSet();
var skippedUserIds = userMap.Keys.ToHashSet();
foreach (var u in await source.Users.IgnoreQueryFilters().AsNoTracking().ToListAsync())
    if (!skippedUserIds.Contains(u.Id) && usedUserIds.Contains(u.Id))
        return Fail("Id tài khoản trùng với tài khoản có sẵn ở đích — dừng để tránh sai lệch. Liên hệ người phát triển.");

// 3. Sắp xếp bảng theo thứ tự khóa ngoại; bỏ mã đặt lại mật khẩu
var ordered = SortByDependency(source.Model.GetEntityTypes().Where(t => t.ClrType != typeof(PasswordResetToken)).ToList());
Console.WriteLine($"Tài khoản có sẵn ở đích được ánh xạ (không chép lại): {userMap.Count}; vai trò: {roleMap.Count}");

var imageCache = new Dictionary<string, string>();
Cloudinary? cloudinary = string.IsNullOrWhiteSpace(cloudinaryUrl) ? null : new Cloudinary(cloudinaryUrl) { Api = { Secure = true } };
var missingFiles = 0; var skippedImages = 0; var uploaded = 0;

// Cột ảnh có thể trỏ tới file trên máy: (kiểu thực thể, tên thuộc tính)
var imageColumns = new (Type, string)[] { (typeof(Product), nameof(Product.MainImage)), (typeof(ProductImage), nameof(ProductImage.ImageUrl)), (typeof(Material), nameof(Material.Image)) };

var plan = new List<(IEntityType Type, List<object> Rows)>();
foreach (var type in ordered)
{
    var rows = (List<object>)await (Task<List<object>>)typeof(Loader).GetMethod(nameof(Loader.LoadAsync))!
        .MakeGenericMethod(type.ClrType).Invoke(null, new object[] { source })!;
    rows = rows.Where(r => !(r is User us && skippedUserIds.Contains(us.Id)) &&
                           !(r is HornViet.Domain.Users.Entities.Role ro && roleMap.ContainsKey(ro.Id)) &&
                           !(r is UserRole urole && skippedUserIds.Contains(urole.UserId))).ToList(); // vai trò của tài khoản đã có ở đích
    plan.Add((type, rows));
    Console.WriteLine($"  {type.GetTableName(),-22} {rows.Count,6} dòng");
}

var localImages = plan.SelectMany(p => p.Rows.SelectMany(r => imageColumns.Where(c => c.Item1 == r.GetType())
        .Select(c => (string?)r.GetType().GetProperty(c.Item2)!.GetValue(r))))
    .Where(IsLocalUpload).Distinct().ToList();
Console.WriteLine($"Ảnh trên máy cần tải lên Cloudinary: {localImages.Count}" + (cloudinary == null && localImages.Count > 0 ? "  (CHƯA có CLOUDINARY_URL — ảnh sẽ giữ đường dẫn cũ và hiển thị lỗi trên cloud)" : ""));

if (!execute) { Console.WriteLine("Chạy thử xong, chưa ghi gì."); return 0; }

// 4. Ghi thật trong một giao dịch
foreach (var (type, rows) in plan)
    foreach (var row in rows)
    {
        Remap(row, userMap, roleMap);
        foreach (var (clr, prop) in imageColumns.Where(c => c.Item1 == type.ClrType))
        {
            var p = clr.GetProperty(prop)!;
            if (p.GetValue(row) is string value && IsLocalUpload(value))
                p.SetValue(row, await ResolveImageAsync(value));
        }
        target.Add(row);
    }

await using (var tx = await target.Database.BeginTransactionAsync())
{
    await target.SaveChangesAsync();
    foreach (var (type, _) in plan.Where(p => p.Type.FindPrimaryKey()?.Properties.Count == 1 && p.Type.FindPrimaryKey()!.Properties[0].Name == "Id"))
    {
        var table = type.GetTableName();
        // Bộ đếm tự tăng của PostgreSQL phải đuổi kịp Id đã chèn tay, nếu không bản ghi mới sẽ trùng khóa.
        await target.Database.ExecuteSqlRawAsync(
            $"SELECT setval(pg_get_serial_sequence('\"{table}\"', 'Id'), COALESCE(MAX(\"Id\"), 1), MAX(\"Id\") IS NOT NULL) FROM \"{table}\"");
    }
    await tx.CommitAsync();
}
Console.WriteLine($"HOÀN TẤT. Ảnh đã tải lên: {uploaded}; ảnh giữ nguyên đường dẫn (mẫu / thiếu Cloudinary): {skippedImages}; file ảnh không thấy trên máy: {missingFiles}.");
return 0;

// ---------------------------------------------------------------------------------------------

static bool IsLocalUpload(string? v) =>
    v is not null && v.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) &&
    !v.StartsWith("/uploads/sample/", StringComparison.OrdinalIgnoreCase); // ảnh mẫu nằm sẵn trong image của backend

static int Fail(string message)
{
    Console.Error.WriteLine("LỖI: " + message);
    return 1;
}

static string FindUploadsRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        if (Directory.Exists(Path.Combine(dir.FullName, "HornViet.Api", "wwwroot")))
            return Path.Combine(dir.FullName, "HornViet.Api", "wwwroot");
    return Path.Combine(Directory.GetCurrentDirectory(), "HornViet.Api", "wwwroot");
}

// Thay Id tài khoản / vai trò đã ánh xạ sang Id ở đích.
static void Remap(object row, Dictionary<long, long> users, Dictionary<int, int> roles)
{
    foreach (var name in new[] { "UserId", "CreatorUserId", "LastModifierUserId" })
    {
        var p = row.GetType().GetProperty(name);
        if (p == null) continue;
        var current = p.GetValue(row);
        if (current is long id && users.TryGetValue(id, out var mapped)) p.SetValue(row, mapped);
        else if (current is long?) { /* LastModifierUserId rỗng giữ nguyên */ }
    }
    if (row is UserRole ur && roles.TryGetValue(ur.RoleId, out var roleId)) ur.RoleId = roleId;
}

static List<IEntityType> SortByDependency(List<IEntityType> types)
{
    var result = new List<IEntityType>();
    var remaining = types.ToList();
    while (remaining.Count > 0)
    {
        var ready = remaining.Where(t => t.GetForeignKeys().All(fk =>
            fk.PrincipalEntityType == t || result.Contains(fk.PrincipalEntityType) || !types.Contains(fk.PrincipalEntityType))).ToList();
        if (ready.Count == 0) throw new InvalidOperationException("Phụ thuộc vòng giữa các bảng.");
        result.AddRange(ready);
        remaining.RemoveAll(ready.Contains);
    }
    return result;
}

async Task<string> ResolveImageAsync(string url)
{
    if (imageCache.TryGetValue(url, out var cached)) return cached;
    if (cloudinary == null) { skippedImages++; return imageCache[url] = url; }

    var path = Path.Combine(uploadsRoot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    if (!File.Exists(path)) { missingFiles++; return imageCache[url] = url; }

    // PublicId theo tên file (đã là GUID) + ghi đè → chạy lại không tạo ảnh trùng.
    var result = await cloudinary.UploadAsync(new ImageUploadParams
    {
        File = new FileDescription(path),
        Folder = cloudinaryFolder,
        PublicId = Path.GetFileNameWithoutExtension(path),
        UseFilename = false,
        UniqueFilename = false,
        Overwrite = true
    });
    if (result.Error != null || result.SecureUrl == null)
        throw new InvalidOperationException($"Tải ảnh lên Cloudinary thất bại: {result.Error?.Message}");

    uploaded++;
    return imageCache[url] = result.SecureUrl.ToString();
}

// Hàm cục bộ của top-level program bị đổi tên khi biên dịch nên không gọi qua reflection được → đặt ở lớp riêng.
static class Loader
{
    public static async Task<List<object>> LoadAsync<T>(DbContext context) where T : class =>
        (await context.Set<T>().IgnoreQueryFilters().AsNoTracking().ToListAsync()).Cast<object>().ToList();
}

using HornViet.Api.Authorization;
using HornViet.Api.Middlewares;
using HornViet.Api.Realtime;
using HornViet.Application;
using HornViet.Application.Common;
using HornViet.Infrastructure;
using HornViet.Infrastructure.Persistence;
using HornViet.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

// Program.cs là điểm khởi động của Web API (tầng Presentation – HornViet.Api), viết theo kiểu "top-level statements":
// không cần tự khai báo lớp Program và hàm Main, trình biên dịch tự sinh ra.
// File gồm hai giai đoạn:
//   1) builder.Services.Add...: đăng ký dịch vụ vào DI container (Dependency Injection) — khai báo ứng dụng "cần gì";
//      sau đó controller/service chỉ việc nhận dịch vụ qua constructor, không tự "new".
//   2) app.Use... / app.Map...: dựng pipeline middleware — mỗi request HTTP đi qua các middleware theo ĐÚNG thứ tự
//      khai báo (từ trên xuống), còn response đi ngược lại (từ dưới lên).

// Tạo builder: tự nạp cấu hình từ appsettings.json → appsettings.{Môi trường}.json → biến môi trường → tham số dòng
// lệnh (nguồn nạp sau ghi đè nguồn nạp trước), đồng thời chuẩn bị logging và web server Kestrel.
var builder = WebApplication.CreateBuilder(args);
// Đối tượng cấu hình dùng chung để đọc các khóa như "ConnectionStrings:Default", "Jwt:SecurityKey", "App:CorsOrigins".
var config = builder.Configuration;

// Nền tảng cloud (Render...) cấp cổng qua biến môi trường PORT và tự lo HTTPS phía trước; máy dev giữ cấu hình
// launchSettings.json như cũ.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Bước 1 – Tầng Infrastructure: đăng ký HornVietDbContext (EF Core; Database:Provider = SqlServer khi phát triển,
// PostgreSql khi triển khai cloud; chuỗi kết nối ConnectionStrings:Default),
// IUnitOfWork và repository generic IRepository<TEntity, TKey>.
builder.Services.AddInfrastructure(config);
// Bước 2 – Tầng Application: đăng ký các Application Service (Account, Profile, Category, Material, Product, Cart,
// Order, User, Statistic), bộ băm mật khẩu PasswordHasher và DataSeeder, vòng đời Scoped (mỗi request một thể hiện).
builder.Services.AddApplication();
// Địa chỉ hai frontend (App:ClientRootAddress, App:AdminRootAddress) để tạo link Quên mật khẩu trong email.
builder.Services.Configure<AppUrlOptions>(config.GetSection("App"));
// Lưu ảnh tải lên: Cloudinary nếu có Cloudinary:Url (staging / production), không thì wwwroot/uploads (dev).
builder.Services.AddFileStorage(config,
    builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
// /health — nền tảng cloud gọi định kỳ để biết ứng dụng còn chạy (không kiểm tra CSDL: database tự tạm dừng khi rảnh).
builder.Services.AddHealthChecks();
// Bước 3 – Xác thực JWT, IAppSession và hai chính sách phân quyền Management / AdminOnly
// (chi tiết trong Authorization/Authentication.cs).
builder.Services.AddJwtAuthentication(config);

// Thời gian thực (WebSocket qua SignalR): đơn mới, trạng thái đơn, tồn kho, thu hồi phiên đăng nhập.
// SignalR có sẵn trong ASP.NET Core; tự lùi về Server-Sent Events / long polling khi mạng chặn WebSocket.
// SignalRNotifier cài đặt IRealtimeNotifier của tầng Application (cùng kiểu IEmailSender ↔ SmtpEmailSender).
builder.Services.AddSignalR();
builder.Services.AddSingleton<ConnectionTracker>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRNotifier>();

// Bước 4 – Đăng ký MVC Controllers:
//   - Filters.Add<WrapResultFilter>(): bộ lọc toàn cục bọc mọi kết quả thành công của controller vào AjaxResponse
//     { result, success: true, error: null, ... } theo định dạng ABP (xem Middlewares/ResponseHandling.cs);
//   - InvalidModelStateResponseFactory: khi [ApiController] phát hiện dữ liệu vào vi phạm DataAnnotations
//     ([Required], [Range], [StringLength]...), thay vì trả ProblemDetails mặc định của ASP.NET Core, ta trả HTTP 400
//     theo định dạng ABP (error.validationErrors[] kèm tên trường members) do ValidationResponse.Create dựng.
builder.Services
    .AddControllers(o => o.Filters.Add<WrapResultFilter>())
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ValidationResponse.Create);

// Bước 5 – CORS (Cross-Origin Resource Sharing). Hai frontend chạy ở origin khác backend — cửa hàng HornViet_customer
// (http://localhost:3000) và trang quản trị HornViet_admin (http://localhost:3001) — nên trình duyệt sẽ CHẶN lời gọi
// API nếu backend không trả header Access-Control-Allow-Origin cho origin đó. Danh sách origin được phép đọc từ
// App:CorsOrigins (các giá trị cách nhau bởi dấu phẩy; bỏ phần tử rỗng và cắt khoảng trắng thừa). Origin phải khớp
// từng ký tự (đúng http/https, đúng cổng, không có "/" cuối); danh sách chỉ nạp lúc khởi động.
var corsOrigins = (config["App:CorsOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
// Chính sách CORS mặc định: chỉ chấp nhận các origin trong danh sách trên, cho phép mọi header (kể cả Authorization
// chứa JWT) và mọi phương thức (GET, POST, PUT, DELETE...). Không bật AllowCredentials vì token gửi qua header,
// không dùng cookie.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// Bước 6 – Swagger (OpenAPI): tự sinh tài liệu mô tả API và trang thử API trực tiếp tại /swagger.
// AddEndpointsApiExplorer giúp Swagger "khám phá" được danh sách endpoint.
builder.Services.AddEndpointsApiExplorer();
// Cấu hình bộ sinh tài liệu Swagger (từng lệnh được giải thích bên trong).
builder.Services.AddSwaggerGen(c =>
{
    // Thông tin chung của tài liệu: tiêu đề và phiên bản v1 (đường dẫn /swagger/v1/swagger.json).
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Hồn Sừng Việt API", Version = "v1" });
    // Khai báo cơ chế bảo mật "Bearer": trang Swagger có nút "Authorize" để dán accessToken; sau đó Swagger tự gửi
    // header "Authorization: Bearer <token>" (kiểu HTTP, scheme bearer, định dạng JWT) trong mọi lần thử API.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Dán accessToken nhận từ /api/TokenAuth/Authenticate",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
    c.CustomSchemaIds(t => t.FullName?.Replace("+", "."));
});

var app = builder.Build();

// Cảnh báo sớm khi cấu hình gửi email (UC Quên mật khẩu) chưa đủ — thay vì chỉ thấy lỗi khi người dùng không nhận được mail.
var smtp = app.Services.GetRequiredService<IOptions<SmtpOptions>>().Value;
foreach (var problem in smtp.GetConfigurationProblems())
    app.Logger.LogWarning("Cấu hình email: {Problem}", problem);
if (!smtp.SendsRealEmail)
    app.Logger.LogInformation("Smtp:Host trống — email Quên mật khẩu được lưu thành file .eml trong {Directory}",
        Path.GetFullPath(smtp.PickupDirectory));

app.Logger.LogInformation("Lưu ảnh tải lên: {Storage}",
    app.Services.GetRequiredService<IFileStorage>() is CloudinaryFileStorage ? "Cloudinary" : "wwwroot/uploads của máy chủ");

if (config.GetValue("Database:MigrateOnStartup", false))
{
    var seed = config.GetSection("Seed").Get<SeedOptions>() ?? new SeedOptions();
    // Staging / production: không tạo tài khoản quản trị với mật khẩu demo có sẵn trong mã nguồn.
    var demo = new SeedOptions();
    if (!app.Environment.IsDevelopment() && (seed.AdminPassword == demo.AdminPassword || seed.StaffPassword == demo.StaffPassword))
        throw new InvalidOperationException(
            "Môi trường không phải Development: đặt Seed__AdminPassword và Seed__StaffPassword (biến môi trường) khác mật khẩu demo.");

    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<HornVietDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync(seed);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.DocumentTitle = "Hồn Sừng Việt API");
}

app.UseStaticFiles(); // wwwroot/uploads — ảnh sản phẩm
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>(NotificationHub.Path);
app.MapHealthChecks("/health");
// Trang gốc: mở địa chỉ backend trên trình duyệt thấy thông tin API thay vì lỗi 404 (backend không có giao diện).
app.MapGet("/", () => Results.Ok(new { service = "Hồn Sừng Việt API", environment = app.Environment.EnvironmentName, health = "/health" }));

app.Run();

public partial class Program;

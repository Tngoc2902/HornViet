using Website.Api.Authorization;
using Website.Api.Middlewares;
using Website.Application;
using Website.Application.Common;
using Website.Infrastructure;
using Website.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

// Program.cs là điểm khởi động của Web API (tầng Presentation – Website.Api), viết theo kiểu "top-level statements":
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

// Bước 1 – Tầng Infrastructure: đăng ký WebsiteDbContext (EF Core + SQL Server, chuỗi kết nối ConnectionStrings:Default),
// IUnitOfWork và repository generic IRepository<TEntity, TKey>.
builder.Services.AddInfrastructure(config);
// Bước 2 – Tầng Application: đăng ký các Application Service (Account, Profile, Category, Material, Product, Cart,
// Order, User, Statistic), bộ băm mật khẩu PasswordHasher và DataSeeder, vòng đời Scoped (mỗi request một thể hiện).
builder.Services.AddApplication();
// Địa chỉ hai frontend (App:ClientRootAddress, App:AdminRootAddress) để tạo link Quên mật khẩu trong email.
builder.Services.Configure<AppUrlOptions>(config.GetSection("App"));
// Bước 3 – Xác thực JWT, IAppSession và hai chính sách phân quyền Management / AdminOnly
// (chi tiết trong Authorization/Authentication.cs).
builder.Services.AddJwtAuthentication(config);

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
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

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

if (config.GetValue("Database:MigrateOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<WebsiteDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DataSeeder>()
        .SeedAsync(config.GetSection("Seed").Get<SeedOptions>() ?? new SeedOptions());
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

app.Run();

public partial class Program;

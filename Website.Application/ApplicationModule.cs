using Website.Application.Accounts;
using Website.Application.Carts;
using Website.Application.Catalog;
using Website.Application.Inventory;
using Website.Application.Orders;
using Website.Application.Profiles;
using Website.Application.Statistics;
using Website.Application.Users;
using Website.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Website.Application;

/// <summary>
/// Điểm đăng ký Dependency Injection (DI) của tầng Application: khai báo cho ASP.NET Core biết
/// "khi một lớp cần interface X thì tạo đối tượng của lớp Y". Nhờ vậy controller chỉ phụ thuộc vào interface
/// (ví dụ IAccountAppService), còn framework tự tạo và truyền đối tượng cài đặt vào hàm khởi tạo.
/// Repository và Unit of Work được đăng ký riêng ở tầng Infrastructure (DependencyInjectionRegister).
/// </summary>
public static class ApplicationModule
{
    /// <summary>
    /// Đăng ký mọi Application Service và các dịch vụ phụ trợ vào container DI.
    /// Được gọi một lần lúc khởi động trong Program.cs: <c>builder.Services.AddApplication();</c>
    /// </summary>
    /// <param name="services">Danh sách dịch vụ của ứng dụng (container DI).</param>
    /// <returns>Chính services để có thể gọi nối tiếp các hàm đăng ký khác.</returns>
    // "this IServiceCollection" biến hàm thành extension method, nên gọi được dạng builder.Services.AddApplication().
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // AddScoped: mỗi HTTP request tạo một đối tượng riêng, dùng chung trong suốt request đó rồi hủy.
        // Chọn Scoped vì các service dùng repository → DbContext (cũng Scoped): mọi service trong cùng một request
        // dùng chung một DbContext, tức chung một Unit of Work, và không lẫn dữ liệu giữa các request khác nhau.

        // Bộ băm mật khẩu của ASP.NET Core Identity (thuật toán PBKDF2 + salt ngẫu nhiên).
        // Dùng chung cho Đăng ký, Đăng nhập, Đổi mật khẩu, Admin tạo tài khoản và DataSeeder.
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        // Mỗi dòng ánh xạ interface → lớp cài đặt của một Application Service (mỗi service điều phối một nhóm use case).
        // Tài khoản: Đăng ký (Bảng 3.3) và kiểm tra thông tin Đăng nhập (Bảng 3.1).
        services.AddScoped<IAccountAppService, AccountAppService>();
        // Hồ sơ cá nhân, đổi mật khẩu, sổ địa chỉ (Bảng 3.4).
        services.AddScoped<IProfileAppService, ProfileAppService>();
        // Quản lý danh mục sản phẩm.
        services.AddScoped<ICategoryAppService, CategoryAppService>();
        // Quản lý nguyên liệu (sừng trâu, sừng bò...) và nguồn gốc.
        services.AddScoped<IMaterialAppService, MaterialAppService>();
        // Sản phẩm, biến thể, tồn kho.
        services.AddScoped<IProductAppService, ProductAppService>();
        // Giỏ hàng.
        services.AddScoped<ICartAppService, CartAppService>();
        // Đặt hàng và xử lý đơn hàng.
        services.AddScoped<IOrderAppService, OrderAppService>();
        // Quản lý người dùng — chỉ Quản trị viên (Bảng 3.13).
        services.AddScoped<IUserAppService, UserAppService>();
        // Thống kê: báo cáo doanh thu, sản phẩm bán chạy, số liệu tổng quan (dashboard).
        services.AddScoped<IStatisticAppService, StatisticAppService>();
        // Nhập kho & giá vốn (chỉ Quản trị viên) — để tính lợi nhuận.
        services.AddScoped<IStockReceiptAppService, StockReceiptAppService>();
        // DataSeeder đăng ký trực tiếp theo lớp (không qua interface) vì chỉ Program.cs dùng nó lúc khởi động
        // để tạo dữ liệu ban đầu.
        services.AddScoped<DataSeeder>();
        // Trả lại services để Program.cs có thể viết nối tiếp (fluent).
        return services;
    }
}

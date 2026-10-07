// Website.Domain.SharedKernel: chứa UserFriendlyException (lỗi nghiệp vụ → HTTP 400).
using Website.Domain.SharedKernel;
// Website.Domain.Users.Enums: chứa enum RoleName (các vai trò Admin / Staff / Customer).
using Website.Domain.Users.Enums;

namespace Website.Application.Common;

// Vì sao cần interface này: tầng Application không được phụ thuộc vào HttpContext/JWT (đó là chi tiết của tầng Api).
// Application chỉ khai báo "tôi cần biết ai đang đăng nhập" qua IAppSession; tầng Website.Api cài đặt nó bằng lớp
// HttpContextAppSession (đọc claim NameIdentifier và Role trong JWT của request hiện tại) và đăng ký vào DI.
// Đây là nguyên lý đảo ngược phụ thuộc (Dependency Inversion) của kiến trúc phân lớp.
// Nhờ đó service luôn lấy Id người dùng từ token đã ký, không tin Id do frontend tự gửi lên.
/// <summary>Thông tin phiên đăng nhập hiện tại (tương đương AbpSession). Cài đặt tại Website.Api từ JWT.</summary>
public interface IAppSession
{
    /// <summary>
    /// Id người dùng đang đăng nhập, lấy từ JWT. Kiểu long? (dấu "?" = có thể null):
    /// null khi request không kèm token hợp lệ (khách vãng lai chưa đăng nhập).
    /// </summary>
    long? UserId { get; }

    /// <summary>Kiểm tra người dùng hiện tại có vai trò roleName hay không (dựa trên claim Role trong JWT).</summary>
    /// <param name="roleName">Tên vai trò, ví dụ "Admin"; nên dùng overload IsInRole(RoleName) bên dưới.</param>
    /// <returns>true nếu token có vai trò đó; false nếu không có hoặc chưa đăng nhập.</returns>
    bool IsInRole(string roleName);
}

/// <summary>
/// Các extension method cho IAppSession. Tham số "this IAppSession session" cho phép gọi
/// <c>_session.GetUserId()</c> như thể đó là phương thức của chính IAppSession,
/// dù IAppSession là interface và không chứa code cài đặt.
/// </summary>
public static class AppSessionExtensions
{
    /// <summary>Lấy Id người dùng đang đăng nhập; bắt buộc phải đăng nhập.</summary>
    /// <param name="session">Phiên đăng nhập hiện tại (được truyền ngầm khi gọi dạng _session.GetUserId()).</param>
    /// <returns>Id người dùng (kiểu long, chắc chắn khác null).</returns>
    /// <exception cref="UserFriendlyException">Chưa đăng nhập → HTTP 400 "Bạn cần đăng nhập...".</exception>
    // Toán tử "??" (null-coalescing): nếu UserId khác null thì trả về giá trị đó,
    // nếu null thì thực hiện vế phải — ở đây là ném ngoại lệ (throw expression).
    // Thực tế các API cần đăng nhập đã có [Authorize] ở controller chặn trước (HTTP 401),
    // dòng này là lớp bảo vệ thứ hai ngay trong tầng Application.
    public static long GetUserId(this IAppSession session) =>
        session.UserId ?? throw new UserFriendlyException("Bạn cần đăng nhập để thực hiện thao tác này.");

    // Dùng ở OrderAppService và ProductAppService để phân biệt người quản lý với khách hàng
    // (ví dụ: người quản lý xem được sản phẩm đang tạm ẩn và đơn hàng của mọi khách).
    /// <summary>Nhân viên hoặc Quản trị viên.</summary>
    /// <param name="session">Phiên đăng nhập hiện tại.</param>
    /// <returns>true nếu người dùng có vai trò Admin hoặc Staff.</returns>
    // "||" là phép HOẶC logic: chỉ cần có một trong hai vai trò là đủ.
    /// <summary>Kiểm tra vai trò theo enum (an toàn kiểu, không phải gõ chuỗi).</summary>
    public static bool IsInRole(this IAppSession session, RoleName role) => session.IsInRole(role.ToString());

    public static bool IsManagement(this IAppSession session) =>
        session.IsInRole(RoleName.Admin) || session.IsInRole(RoleName.Staff);
}

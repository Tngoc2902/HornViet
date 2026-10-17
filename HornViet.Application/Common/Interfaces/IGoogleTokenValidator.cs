namespace HornViet.Application.Common;

/// <summary>
/// Kiểm tra access token Google mà trang cửa hàng nhận được khi khách bấm "Đăng nhập bằng Google". Interface ở tầng
/// Application, cài đặt gọi máy chủ Google ở tầng Infrastructure (Services/GoogleTokenValidator).
/// </summary>
public interface IGoogleTokenValidator
{
    /// <summary>Đã cấu hình Google:ClientId — chưa cấu hình thì tắt đăng nhập bằng Google.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Hỏi Google token còn hiệu lực và đúng là cấp cho ứng dụng này (audience = ClientId), rồi lấy thông tin tài khoản.
    /// Token không hợp lệ → null.
    /// </summary>
    Task<GoogleUserInfo?> ValidateAsync(string accessToken, CancellationToken ct = default);
}

/// <summary>Thông tin tài khoản Google lấy được từ token đã kiểm tra.</summary>
/// <param name="Subject">Mã định danh cố định của tài khoản Google (claim "sub").</param>
/// <param name="Email">Email của tài khoản Google.</param>
/// <param name="EmailVerified">Google đã xác minh email này.</param>
/// <param name="Name">Họ tên hiển thị trên Google (có thể trống).</param>
public record GoogleUserInfo(string Subject, string Email, bool EmailVerified, string? Name);

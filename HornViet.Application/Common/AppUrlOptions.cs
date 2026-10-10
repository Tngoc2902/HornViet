namespace HornViet.Application.Common;

/// <summary>
/// Địa chỉ hai frontend (mục "App" trong appsettings.json), dùng để tạo link trong email.
/// Link luôn lấy từ cấu hình, không lấy từ request, để kẻ gian không thể đổi tên miền trong link.
/// </summary>
public class AppUrlOptions
{
    /// <summary>Trang cửa hàng (HornViet_customer), VD http://localhost:3000/.</summary>
    public string ClientRootAddress { get; set; } = "";

    /// <summary>Trang quản trị (HornViet_admin), VD http://localhost:3001/ — link của Nhân viên / Quản trị viên.</summary>
    public string AdminRootAddress { get; set; } = "";
}

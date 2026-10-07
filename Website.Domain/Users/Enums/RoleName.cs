namespace Website.Domain.Users.Enums;

/// <summary>
/// Các vai trò cố định của hệ thống. Tên enum chính là giá trị lưu ở AbpRoles.Name, claim Role trong JWT và
/// chuỗi frontend so sánh ("Admin", "Staff", "Customer") — <b>không đổi tên</b> các giá trị này.
/// Cần chuỗi hằng (truy vấn EF, chính sách phân quyền, pattern matching) thì dùng <c>nameof(RoleName.Admin)</c>.
/// </summary>
public enum RoleName
{
    /// <summary>Quản trị viên: toàn quyền, kể cả quản lý người dùng và thống kê doanh thu.</summary>
    Admin,

    /// <summary>Nhân viên: quản lý danh mục, sản phẩm, tồn kho, đơn hàng, dashboard.</summary>
    Staff,

    /// <summary>Khách hàng: giỏ hàng, đặt hàng, đơn của tôi, hồ sơ. Tài khoản tự đăng ký luôn có vai trò này.</summary>
    Customer
}

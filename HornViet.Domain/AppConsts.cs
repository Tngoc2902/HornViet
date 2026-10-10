namespace HornViet.Domain;

/// <summary>
/// Các hằng số dùng chung của hệ thống, đặt ở tầng Domain để mọi tầng phía trên (Application, Api) dùng chung
/// một giá trị, tránh "số ma thuật" (magic number) rải rác và lệch nhau giữa các nơi.
/// </summary>
// static class: lớp tĩnh, không thể tạo đối tượng bằng new; chỉ chứa thành viên tĩnh, truy cập trực tiếp
// qua tên lớp, VD AppConsts.MaxPageSize.
// const: hằng số được thay giá trị ngay lúc biên dịch; nhờ vậy dùng được trong attribute như
// [RegularExpression(AppConsts.PhoneRegex)] hay [Range(1, AppConsts.MaxPageSize)].
public static class AppConsts
{
    // Dùng ở ProductAppService (lọc/gắn cờ IsLowStock cho sản phẩm có biến thể sắp hết) và StatisticAppService
    // (danh sách biến thể sắp hết hàng của sản phẩm đang bán) — hỗ trợ nhân viên chủ động nhập thêm hàng.
    /// <summary>Biến thể có tồn kho &lt;= ngưỡng này được gắn nhãn "Sắp hết hàng" ở trang quản trị.</summary>
    public const int LowStockThreshold = 5;

    /// <summary>
    /// Số bản ghi mặc định trên một trang khi client không truyền kích thước trang (giá trị mặc định của
    /// MaxResultCount trong PagedInput).
    /// </summary>
    public const int DefaultPageSize = 12;
    /// <summary>
    /// Kích thước trang tối đa cho phép (ràng buộc [Range] của MaxResultCount) — chặn client yêu cầu quá nhiều
    /// bản ghi một lúc làm chậm máy chủ và CSDL.
    /// </summary>
    public const int MaxPageSize = 100;

    // Giải thích biểu thức chính quy (regex):
    // ^ và $: khớp từ đầu đến cuối chuỗi (không cho thừa ký tự).
    // (0|\+84): bắt đầu bằng 0 hoặc +84 (dấu + phải viết \+ vì + là ký tự đặc biệt trong regex).
    // (3|5|7|8|9): chữ số đầu mạng di động Việt Nam hiện hành.
    // \d{8}: tiếp theo đúng 8 chữ số; VD 0912345678 (10 số) hoặc +84912345678.
    // Dấu @ trước chuỗi: chuỗi nguyên văn (verbatim string), dấu \ không bị hiểu là ký tự thoát của C#.
    // Được dùng trong các DTO đăng ký, hồ sơ, sổ địa chỉ, đặt hàng, quản lý người dùng qua [RegularExpression].
    /// <summary>Số di động Việt Nam: 0xxxxxxxxx hoặc +84xxxxxxxxx.</summary>
    public const string PhoneRegex = @"^(0|\+84)(3|5|7|8|9)\d{8}$";

    // Dùng ở AccountAppService (UC Quên mật khẩu): link trong email chỉ dùng được trong khoảng thời gian này.
    /// <summary>Thời hạn (phút) của link đặt lại mật khẩu gửi qua email.</summary>
    public const int PasswordResetTokenLifetimeMinutes = 30;

    // Chống spam email: trong khoảng này, yêu cầu Quên mật khẩu lặp lại cho cùng tài khoản không gửi thêm email.
    /// <summary>Số giây tối thiểu giữa hai lần gửi email đặt lại mật khẩu cho cùng một tài khoản.</summary>
    public const int PasswordResetResendSeconds = 60;
}

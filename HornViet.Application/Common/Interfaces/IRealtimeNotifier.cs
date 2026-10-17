using HornViet.Domain.Orders.Enums;

namespace HornViet.Application.Common;

/// <summary>
/// Đẩy thông báo thời gian thực tới giao diện (đơn mới, đổi trạng thái đơn, tồn kho, thu hồi phiên đăng nhập).
/// Interface ở tầng Application, cài đặt bằng SignalR (WebSocket) ở tầng Api (Realtime/SignalRNotifier) — giống IEmailSender,
/// Application không phụ thuộc thư viện SignalR.
/// </summary>
/// <remarks>
/// Chỉ gọi SAU khi <c>IUnitOfWork.SaveChangesAsync</c> thành công để không báo một thay đổi chưa được lưu.
/// Cài đặt không được ném lỗi: gửi thông báo thất bại không làm hỏng thao tác nghiệp vụ đã lưu.
/// Payload không chứa thông tin cá nhân của khách (tên, số điện thoại, địa chỉ).
/// </remarks>
public interface IRealtimeNotifier
{
    /// <summary>Khách vừa đặt đơn → báo Nhân viên / Quản trị viên.</summary>
    Task OrderCreatedAsync(OrderCreatedEvent e);

    /// <summary>Đơn đổi trạng thái (giao hàng, hoàn thành, hủy) → báo chủ đơn và Nhân viên / Quản trị viên.</summary>
    Task OrderStatusChangedAsync(OrderStatusChangedEvent e);

    /// <summary>
    /// Tồn kho biến thể thay đổi → báo người đang xem sản phẩm đó.
    /// <paramref name="alertLowStock"/> = true (khi bán hàng): biến thể còn ≤ AppConsts.LowStockThreshold được báo cho quản trị.
    /// </summary>
    Task StockChangedAsync(IReadOnlyCollection<StockChange> changes, bool alertLowStock = false);

    /// <summary>Thông tin sản phẩm đổi (sửa, ẩn / hiện, xóa) → trang chi tiết đang mở tự tải lại.</summary>
    Task ProductChangedAsync(int productId);

    /// <summary>Phiên đăng nhập của tài khoản bị thu hồi (khóa, đổi vai trò, đổi / đặt lại mật khẩu) → đăng xuất ngay.</summary>
    Task SessionRevokedAsync(long userId, SessionRevokedReason reason);
}

/// <summary>Đơn mới: chỉ mã đơn, số lượng sản phẩm và tổng tiền.</summary>
public record OrderCreatedEvent(int OrderId, string Code, int ItemCount, decimal TotalAmount, DateTime CreationTime);

/// <summary><paramref name="UserId"/> dùng để chọn người nhận (chủ đơn), không gửi ra giao diện.</summary>
public record OrderStatusChangedEvent(int OrderId, string Code, long UserId, OrderStatus Status);

/// <summary>Tồn kho mới của một biến thể; tên sản phẩm / biến thể dùng cho cảnh báo sắp hết hàng.</summary>
public record StockChange(int ProductId, int VariantId, int StockQuantity, string ProductName, string VariantName);

public enum SessionRevokedReason
{
    Locked,          // Quản trị viên khóa tài khoản
    RolesChanged,    // Quản trị viên đổi vai trò
    PasswordReset,   // Mật khẩu được đặt lại (bởi Quản trị viên hoặc qua email)
    PasswordChanged  // Người dùng tự đổi mật khẩu
}

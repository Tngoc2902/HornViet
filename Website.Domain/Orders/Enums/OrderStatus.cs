namespace Website.Domain.Orders.Enums;

/// <summary>Trạng thái đơn hàng. Chuyển trạng thái hợp lệ do Order.ChangeStatus kiểm soát.</summary>
public enum OrderStatus
{
    Pending = 0,    // Chờ xử lý
    Shipping = 1,   // Đang giao
    Completed = 2,  // Hoàn thành
    Canceled = 3    // Đã hủy
}

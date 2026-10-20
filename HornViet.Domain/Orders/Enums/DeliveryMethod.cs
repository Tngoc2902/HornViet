namespace HornViet.Domain.Orders.Enums;

/// <summary>Hình thức nhận hàng khách chọn khi đặt.</summary>
public enum DeliveryMethod
{
    Shipping = 0,    // Giao tận nơi (Giao Hàng Nhanh)
    StorePickup = 1  // Nhận tại cửa hàng: không phí vận chuyển, không tạo vận đơn
}

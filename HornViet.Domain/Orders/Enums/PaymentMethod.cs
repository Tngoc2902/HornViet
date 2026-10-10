namespace HornViet.Domain.Orders.Enums;

/// <summary>Phương thức thanh toán khách chọn khi đặt hàng.</summary>
public enum PaymentMethod
{
    COD = 0,          // Thanh toán khi nhận hàng
    BankTransfer = 1  // Chuyển khoản (chỉ ghi nhận, chưa tích hợp cổng thanh toán)
}

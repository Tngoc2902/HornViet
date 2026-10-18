namespace HornViet.Application.Common;

/// <summary>
/// Đơn vị vận chuyển (hiện là Giao Hàng Nhanh). Interface ở tầng Application, cài đặt gọi API ở tầng Infrastructure
/// (Services/GhnShippingProvider) — Application không phụ thuộc cách gọi cụ thể của từng hãng.
/// </summary>
public interface IShippingProvider
{
    /// <summary>Đã cấu hình Token / ShopId; chưa thì tính năng vận chuyển tắt và đặt hàng chạy như cũ (phí ship = 0).</summary>
    bool IsEnabled { get; }

    /// <summary>Đang dùng môi trường thử của đơn vị vận chuyển (không có shipper thật, trạng thái không tự đổi).</summary>
    bool IsSandbox { get; }

    /// <summary>Cân nặng mặc định của một sản phẩm (gam) khi sản phẩm chưa khai báo cân nặng riêng.</summary>
    int DefaultItemWeightGrams { get; }

    /// <summary>Tỉnh / thành theo đơn vị hành chính mới 2 cấp (sau sáp nhập 01/07/2025).</summary>
    Task<IReadOnlyList<ShippingProvince>> GetProvincesAsync(CancellationToken ct = default);

    /// <summary>Phường / xã (mới, không còn cấp quận / huyện) của một tỉnh / thành.</summary>
    Task<IReadOnlyList<ShippingWard>> GetWardsAsync(int provinceId, CancellationToken ct = default);

    /// <summary>Phí vận chuyển (đồng) từ kho của shop tới địa chỉ nhận.</summary>
    Task<ShippingFee> CalculateFeeAsync(ShippingFeeRequest request, CancellationToken ct = default);

    /// <summary>Tạo vận đơn; lỗi của đơn vị vận chuyển được ném kèm lý do để nhân viên biết sửa gì.</summary>
    Task<ShipmentCreated> CreateShipmentAsync(ShipmentRequest request, CancellationToken ct = default);

    /// <summary>Tình trạng vận đơn hiện tại (dùng khi làm mới thủ công, phòng khi webhook chưa tới).</summary>
    Task<ShipmentInfo> GetShipmentAsync(string orderCode, CancellationToken ct = default);

    /// <summary>Hủy vận đơn; đơn đã được lấy hàng thì đơn vị vận chuyển từ chối và hàm ném lỗi.</summary>
    Task CancelShipmentAsync(string orderCode, string reason, CancellationToken ct = default);

    /// <summary>Đường dẫn mở phiếu gửi hàng (A5) để in.</summary>
    Task<string> GetPrintUrlAsync(string orderCode, CancellationToken ct = default);
}

public record ShippingProvince(int Id, string Name);

public record ShippingWard(string Code, int ProvinceId, string Name);

/// <param name="ToProvinceName">Tên tỉnh / thành chuẩn (lấy từ danh mục mới).</param>
/// <param name="ToWardName">Tên phường / xã chuẩn (lấy từ danh mục mới).</param>
/// <param name="WeightGrams">Tổng cân nặng (gam).</param>
/// <param name="InsuranceValue">Giá trị khai báo bảo hiểm (đồng) — tiền hàng của đơn.</param>
public record ShippingFeeRequest(string ToProvinceName, string ToWardName, int WeightGrams, decimal InsuranceValue);

/// <param name="Total">Tổng phí vận chuyển (đồng), đã gồm phí bảo hiểm.</param>
public record ShippingFee(decimal Total);

/// <param name="ClientOrderCode">Mã đơn của hệ thống (HSV000123), duy nhất trong shop.</param>
/// <param name="ToAddress">Số nhà, tên đường (không gồm phường / tỉnh — gửi riêng ở ToWardName / ToProvinceName).</param>
/// <param name="CodAmount">Số tiền thu hộ (đồng); 0 nếu khách đã thanh toán.</param>
/// <param name="InsuranceValue">Giá trị khai báo bảo hiểm (đồng).</param>
public record ShipmentRequest(string ClientOrderCode, string ToName, string ToPhone, string ToAddress, string ToProvinceName,
    string ToWardName, decimal CodAmount, decimal InsuranceValue, int WeightGrams, string Content, string? Note,
    IReadOnlyList<ShipmentItem> Items);

public record ShipmentItem(string Name, int Quantity, decimal Price, int WeightGrams);

/// <param name="OrderCode">Mã vận đơn của đơn vị vận chuyển.</param>
public record ShipmentCreated(string OrderCode, DateTime? ExpectedDeliveryTime, decimal TotalFee);

/// <param name="Status">Mã trạng thái của đơn vị vận chuyển (ready_to_pick, delivering, delivered…).</param>
public record ShipmentInfo(string Status, DateTime? UpdatedTime, DateTime? ExpectedDeliveryTime);

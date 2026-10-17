namespace HornViet.Application.Common;

/// <summary>
/// Đơn vị vận chuyển (hiện là Giao Hàng Nhanh). Interface ở tầng Application, cài đặt gọi API ở tầng Infrastructure
/// (Services/GhnShippingProvider) — Application không phụ thuộc cách gọi cụ thể của từng hãng.
/// </summary>
public interface IShippingProvider
{
    /// <summary>Đã cấu hình Token / ShopId; chưa thì tính năng vận chuyển tắt và đặt hàng chạy như cũ (phí ship = 0).</summary>
    bool IsEnabled { get; }

    /// <summary>Cân nặng mặc định của một sản phẩm (gam) khi sản phẩm chưa khai báo cân nặng riêng.</summary>
    int DefaultItemWeightGrams { get; }

    Task<IReadOnlyList<ShippingProvince>> GetProvincesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ShippingDistrict>> GetDistrictsAsync(int provinceId, CancellationToken ct = default);
    Task<IReadOnlyList<ShippingWard>> GetWardsAsync(int districtId, CancellationToken ct = default);

    /// <summary>Phí vận chuyển (đồng) từ kho của shop tới địa chỉ nhận.</summary>
    Task<ShippingFee> CalculateFeeAsync(ShippingFeeRequest request, CancellationToken ct = default);
}

public record ShippingProvince(int Id, string Name);

public record ShippingDistrict(int Id, int ProvinceId, string Name);

public record ShippingWard(string Code, int DistrictId, string Name);

/// <param name="ToDistrictId">Mã quận / huyện người nhận (GHN DistrictID).</param>
/// <param name="ToWardCode">Mã phường / xã người nhận (GHN WardCode).</param>
/// <param name="WeightGrams">Tổng cân nặng (gam).</param>
/// <param name="InsuranceValue">Giá trị khai báo bảo hiểm (đồng) — tiền hàng của đơn.</param>
public record ShippingFeeRequest(int ToDistrictId, string ToWardCode, int WeightGrams, decimal InsuranceValue);

/// <param name="Total">Tổng phí vận chuyển (đồng), đã gồm phí bảo hiểm.</param>
public record ShippingFee(decimal Total);

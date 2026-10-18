using System.ComponentModel.DataAnnotations;
using HornViet.Application.Carts;
using HornViet.Application.Common;
using HornViet.Domain.SharedKernel;

namespace HornViet.Application.Shipping;

/// <summary>Tỉnh / thành phố để khách chọn địa chỉ nhận hàng.</summary>
public class ProvinceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

public class DistrictDto
{
    public int Id { get; set; }
    public int ProvinceId { get; set; }
    public string Name { get; set; } = null!;
}

public class WardDto
{
    public string Code { get; set; } = null!;
    public int DistrictId { get; set; }
    public string Name { get; set; } = null!;
}

/// <summary>Địa chỉ nhận hàng cần tính phí ship (chọn từ danh sách Quận / Phường của đơn vị vận chuyển).</summary>
public class CalculateShippingFeeInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn quận / huyện.")]
    public int ToDistrictId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phường / xã.")]
    public string ToWardCode { get; set; } = null!;
}

public class ShippingFeeDto
{
    /// <summary>false = chưa cấu hình đơn vị vận chuyển: phí ship luôn 0 và form đặt hàng không cần chọn Quận / Phường.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Phí vận chuyển (đồng) cho giỏ hàng hiện tại.</summary>
    public decimal Fee { get; set; }
}

public interface IShippingAppService
{
    /// <summary>Đã bật vận chuyển (frontend dùng để quyết định có hiện bộ chọn Tỉnh / Quận / Phường không).</summary>
    bool IsEnabled { get; }

    Task<IReadOnlyList<ProvinceDto>> GetProvincesAsync();
    Task<IReadOnlyList<DistrictDto>> GetDistrictsAsync(int provinceId);
    Task<IReadOnlyList<WardDto>> GetWardsAsync(int districtId);

    /// <summary>Phí ship cho giỏ hàng của người dùng hiện tại tới địa chỉ đã chọn.</summary>
    Task<ShippingFeeDto> CalculateFeeAsync(CalculateShippingFeeInput input);

    /// <summary>
    /// Dùng khi đặt hàng: kiểm tra Tỉnh / Quận / Phường khách chọn thuộc nhau (theo danh mục của đơn vị vận chuyển, không tin
    /// tên frontend gửi), lấy tên chuẩn để ghép địa chỉ và tính phí ship cho đúng số lượng / tiền hàng của đơn.
    /// </summary>
    Task<ShippingQuote> QuoteAsync(int provinceId, int districtId, string wardCode, int quantity, decimal subTotal);
}

/// <summary>Kết quả kiểm tra địa chỉ và phí ship của một đơn.</summary>
public record ShippingQuote(decimal Fee, string ProvinceName, string DistrictName, string WardName);

/// <summary>
/// Danh mục địa chỉ và phí vận chuyển. Phí ship luôn do server tính từ giỏ hàng — không tin số tiền frontend gửi lên.
/// </summary>
public class ShippingAppService : IShippingAppService
{
    // GHN giao hàng nhẹ (service_type_id = 2) cho kiện dưới 20 kg.
    private const int MaxLightWeightGrams = 20_000;

    private readonly IShippingProvider _provider;
    private readonly ICartAppService _cartService;

    public ShippingAppService(IShippingProvider provider, ICartAppService cartService)
    {
        _provider = provider;
        _cartService = cartService;
    }

    public bool IsEnabled => _provider.IsEnabled;

    public async Task<IReadOnlyList<ProvinceDto>> GetProvincesAsync()
    {
        EnsureEnabled();
        var rows = await _provider.GetProvincesAsync();
        return rows.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new ProvinceDto { Id = p.Id, Name = p.Name }).ToList();
    }

    public async Task<IReadOnlyList<DistrictDto>> GetDistrictsAsync(int provinceId)
    {
        EnsureEnabled();
        var rows = await _provider.GetDistrictsAsync(provinceId);
        return rows.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(d => new DistrictDto { Id = d.Id, ProvinceId = d.ProvinceId, Name = d.Name }).ToList();
    }

    public async Task<IReadOnlyList<WardDto>> GetWardsAsync(int districtId)
    {
        EnsureEnabled();
        var rows = await _provider.GetWardsAsync(districtId);
        return rows.OrderBy(w => w.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(w => new WardDto { Code = w.Code, DistrictId = w.DistrictId, Name = w.Name }).ToList();
    }

    public async Task<ShippingFeeDto> CalculateFeeAsync(CalculateShippingFeeInput input)
    {
        if (!_provider.IsEnabled)
            return new ShippingFeeDto { IsEnabled = false, Fee = 0 };

        var cart = await _cartService.GetMyCartAsync();
        var items = cart.Items.Where(i => i.IsAvailable).ToList();
        if (items.Count == 0)
            throw new UserFriendlyException("Giỏ hàng trống hoặc sản phẩm đã hết hàng.");

        var fee = await FeeAsync(input.ToDistrictId, input.ToWardCode.Trim(), items.Sum(i => i.Quantity), items.Sum(i => i.LineTotal));
        return new ShippingFeeDto { IsEnabled = true, Fee = fee };
    }

    public async Task<ShippingQuote> QuoteAsync(int provinceId, int districtId, string wardCode, int quantity, decimal subTotal)
    {
        EnsureEnabled();
        wardCode = wardCode.Trim();

        // Tên địa chỉ lấy từ danh mục chuẩn và phải khớp nhau — khách không thể ghép quận của tỉnh này với phường của tỉnh khác.
        var province = (await _provider.GetProvincesAsync()).FirstOrDefault(p => p.Id == provinceId)
                       ?? throw new UserFriendlyException("Tỉnh / thành phố không hợp lệ.");
        var district = (await _provider.GetDistrictsAsync(provinceId)).FirstOrDefault(d => d.Id == districtId)
                       ?? throw new UserFriendlyException("Quận / huyện không thuộc tỉnh / thành phố đã chọn.");
        var ward = (await _provider.GetWardsAsync(districtId)).FirstOrDefault(w => w.Code == wardCode)
                   ?? throw new UserFriendlyException("Phường / xã không thuộc quận / huyện đã chọn.");

        var fee = await FeeAsync(districtId, wardCode, quantity, subTotal);
        return new ShippingQuote(fee, province.Name, district.Name, ward.Name);
    }

    private async Task<decimal> FeeAsync(int districtId, string wardCode, int quantity, decimal subTotal)
    {
        var weight = quantity * _provider.DefaultItemWeightGrams;
        if (weight >= MaxLightWeightGrams)
            throw new UserFriendlyException("Đơn hàng quá nặng để giao nhanh qua hệ thống. Vui lòng giảm số lượng hoặc liên hệ cửa hàng.");

        var fee = await _provider.CalculateFeeAsync(new ShippingFeeRequest(districtId, wardCode, weight, subTotal));
        return fee.Total;
    }

    private void EnsureEnabled()
    {
        if (!_provider.IsEnabled)
            throw new UserFriendlyException("Cửa hàng chưa bật dịch vụ giao hàng.");
    }
}

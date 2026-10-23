using System.ComponentModel.DataAnnotations;
using HornViet.Application.Carts;
using HornViet.Application.Common;
using HornViet.Domain;
using HornViet.Domain.SharedKernel;

namespace HornViet.Application.Shipping;

/// <summary>Tỉnh / thành phố để khách chọn địa chỉ nhận hàng.</summary>
public class ProvinceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

public class WardDto
{
    public string Code { get; set; } = null!;
    public int ProvinceId { get; set; }
    public string Name { get; set; } = null!;
}

/// <summary>Địa chỉ nhận hàng cần tính phí ship (chọn từ danh sách Quận / Phường của đơn vị vận chuyển).</summary>
public class CalculateShippingFeeInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tỉnh / thành phố.")]
    public int ToProvinceId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phường / xã.")]
    public string ToWardCode { get; set; } = null!;
}

public class ShippingFeeDto
{
    /// <summary>false = chưa cấu hình đơn vị vận chuyển: phí ship luôn 0 và form đặt hàng không cần chọn Quận / Phường.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Phí vận chuyển (đồng) khách phải trả cho giỏ hàng hiện tại (0 khi được miễn phí).</summary>
    public decimal Fee { get; set; }

    /// <summary>Tiền hàng đạt <see cref="FreeShippingThreshold"/> → miễn phí giao hàng.</summary>
    public bool FreeShipping { get; set; }

    /// <summary>Mức tiền hàng được miễn phí giao hàng (giao diện hiện "mua thêm … để được miễn phí").</summary>
    public decimal FreeShippingThreshold { get; set; } = AppConsts.FreeShippingThreshold;
}

public interface IShippingAppService
{
    /// <summary>Đã bật vận chuyển (frontend dùng để quyết định có hiện bộ chọn Tỉnh / Quận / Phường không).</summary>
    bool IsEnabled { get; }

    /// <summary>Môi trường thử của đơn vị vận chuyển — trang quản trị hiện công cụ giả lập trạng thái giao hàng.</summary>
    bool IsSandbox { get; }

    Task<IReadOnlyList<ProvinceDto>> GetProvincesAsync();
    Task<IReadOnlyList<WardDto>> GetWardsAsync(int provinceId);

    /// <summary>Phí ship cho giỏ hàng của người dùng hiện tại tới địa chỉ đã chọn.</summary>
    Task<ShippingFeeDto> CalculateFeeAsync(CalculateShippingFeeInput input);

    /// <summary>
    /// Dùng khi đặt hàng: kiểm tra Tỉnh / Quận / Phường khách chọn thuộc nhau (theo danh mục của đơn vị vận chuyển, không tin
    /// tên frontend gửi), lấy tên chuẩn để ghép địa chỉ và tính phí ship cho đúng số lượng / tiền hàng của đơn.
    /// </summary>
    Task<ShippingQuote> QuoteAsync(int provinceId, string wardCode, int quantity, decimal subTotal);

    /// <summary>Tên chuẩn của tỉnh / phường theo mã (dùng khi tạo vận đơn).</summary>
    Task<(string ProvinceName, string WardName)> ResolveNamesAsync(int provinceId, string wardCode);
}

/// <summary>Kết quả kiểm tra địa chỉ và phí ship của một đơn. <paramref name="FreeShipping"/>: đơn đủ mức miễn phí (Fee = 0).</summary>
public record ShippingQuote(decimal Fee, string ProvinceName, string WardName, bool FreeShipping = false);

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

    public bool IsSandbox => _provider.IsEnabled && _provider.IsSandbox;

    public async Task<IReadOnlyList<ProvinceDto>> GetProvincesAsync()
    {
        EnsureEnabled();
        var rows = await _provider.GetProvincesAsync();
        return rows.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new ProvinceDto { Id = p.Id, Name = p.Name }).ToList();
    }

    public async Task<IReadOnlyList<WardDto>> GetWardsAsync(int provinceId)
    {
        EnsureEnabled();
        var rows = await _provider.GetWardsAsync(provinceId);
        return rows.OrderBy(w => w.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(w => new WardDto { Code = w.Code, ProvinceId = w.ProvinceId, Name = w.Name }).ToList();
    }

    public async Task<ShippingFeeDto> CalculateFeeAsync(CalculateShippingFeeInput input)
    {
        if (!_provider.IsEnabled)
            return new ShippingFeeDto { IsEnabled = false, Fee = 0 };

        var cart = await _cartService.GetMyCartAsync();
        var items = cart.Items.Where(i => i.IsAvailable).ToList();
        if (items.Count == 0)
            throw new UserFriendlyException("Giỏ hàng trống hoặc sản phẩm đã hết hàng.");

        var quote = await QuoteAsync(input.ToProvinceId, input.ToWardCode, items.Sum(i => i.Quantity), items.Sum(i => i.LineTotal));
        return new ShippingFeeDto { IsEnabled = true, Fee = quote.Fee, FreeShipping = quote.FreeShipping };
    }

    public async Task<ShippingQuote> QuoteAsync(int provinceId, string wardCode, int quantity, decimal subTotal)
    {
        EnsureEnabled();
        var weight = quantity * _provider.DefaultItemWeightGrams;
        if (weight >= MaxLightWeightGrams)
            throw new UserFriendlyException("Đơn hàng quá nặng để giao nhanh qua hệ thống. Vui lòng giảm số lượng hoặc liên hệ cửa hàng.");

        // Tên địa chỉ lấy từ danh mục chuẩn và phải khớp nhau — khách không thể ghép phường của tỉnh này với tỉnh khác.
        var (provinceName, wardName) = await ResolveNamesAsync(provinceId, wardCode);
        // Tiền hàng đủ mức → khách không trả phí ship (cửa hàng trả cước GHN); không cần hỏi giá GHN.
        if (subTotal >= AppConsts.FreeShippingThreshold)
            return new ShippingQuote(0, provinceName, wardName, FreeShipping: true);
        var fee = await _provider.CalculateFeeAsync(new ShippingFeeRequest(provinceName, wardName, weight, subTotal));
        return new ShippingQuote(fee.Total, provinceName, wardName);
    }

    public async Task<(string ProvinceName, string WardName)> ResolveNamesAsync(int provinceId, string wardCode)
    {
        wardCode = wardCode.Trim();
        var province = (await _provider.GetProvincesAsync()).FirstOrDefault(p => p.Id == provinceId)
                       ?? throw new UserFriendlyException("Tỉnh / thành phố không hợp lệ.");
        var ward = (await _provider.GetWardsAsync(provinceId)).FirstOrDefault(w => w.Code == wardCode)
                   ?? throw new UserFriendlyException("Phường / xã không thuộc tỉnh / thành phố đã chọn.");
        return (province.Name, ward.Name);
    }

    private void EnsureEnabled()
    {
        if (!_provider.IsEnabled)
            throw new UserFriendlyException("Cửa hàng chưa bật dịch vụ giao hàng.");
    }
}

using HornViet.Application.Shipping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HornViet.Api.Controllers.Shopping;

/// <summary>
/// Giao hàng: danh mục Tỉnh / Phường (công khai, dữ liệu của đơn vị vận chuyển) và tính phí ship (cần đăng nhập vì
/// tính theo giỏ hàng). Token của đơn vị vận chuyển chỉ nằm ở backend, frontend không bao giờ thấy.
/// </summary>
public class ShippingController : HornVietBaseController
{
    private readonly IShippingAppService _service;

    public ShippingController(IShippingAppService service) => _service = service;

    /// <summary>Bật hay tắt tích hợp vận chuyển — frontend quyết định có hiện bộ chọn Tỉnh / Quận / Phường không.</summary>
    [HttpGet]
    public ActionResult<object> GetStatus() => Ok(new { isEnabled = _service.IsEnabled });

    [HttpGet]
    public Task<ActionResult<IReadOnlyList<ProvinceDto>>> GetProvinces() => Run(() => _service.GetProvincesAsync());

    /// <summary>Phường / xã của một tỉnh / thành (địa chỉ mới 2 cấp, không còn quận / huyện).</summary>
    [HttpGet]
    public Task<ActionResult<IReadOnlyList<WardDto>>> GetWards([FromQuery] int provinceId) =>
        Run(() => _service.GetWardsAsync(provinceId));

    /// <summary>Phí ship cho giỏ hàng của người dùng hiện tại tới địa chỉ đã chọn.</summary>
    [Authorize]
    [HttpPost]
    public Task<ActionResult<ShippingFeeDto>> CalculateFee([FromBody] CalculateShippingFeeInput input) =>
        Run(() => _service.CalculateFeeAsync(input));
}

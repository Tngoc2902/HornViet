using HornViet.Api.Authorization;
using HornViet.Application.Common;
using HornViet.Application.Shipping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HornViet.Api.Controllers.Shopping;

/// <summary>
/// Vận đơn của đơn hàng: nhân viên tạo / hủy / in; chủ đơn và nhân viên xem tiến trình giao hàng (có thể làm mới thủ công
/// từ đơn vị vận chuyển phòng khi webhook chưa tới).
/// </summary>
public class ShipmentController : HornVietBaseController
{
    private readonly IShipmentAppService _service;

    public ShipmentController(IShipmentAppService service) => _service = service;

    [HttpPost, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ActionResult<ShipmentDto>> Create([FromBody] EntityDto<int> input) => Run(() => _service.CreateAsync(input.Id));

    [HttpPost, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ActionResult<ShipmentDto>> Cancel([FromBody] EntityDto<int> input) => Run(() => _service.CancelAsync(input.Id));

    [HttpGet, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ActionResult<string>> GetLabelUrl([FromQuery] int orderId) => Run(() => _service.GetLabelUrlAsync(orderId));

    [HttpGet, Authorize]
    public Task<ActionResult<ShipmentDto>> Get([FromQuery] int orderId, [FromQuery] bool refresh = false) =>
        Run(() => _service.GetAsync(orderId, refresh));
}

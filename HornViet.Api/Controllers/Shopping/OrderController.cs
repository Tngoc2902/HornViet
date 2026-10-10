using HornViet.Api.Authorization;
using HornViet.Application.Common;
using HornViet.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HornViet.Api.Controllers.Shopping;

/// <summary>UC Đặt hàng, Quản lý đơn hàng cá nhân (khách hàng) và Quản lý đơn hàng (quản trị).</summary>
[Authorize]
public class OrderController : HornVietBaseController
{
    private readonly IOrderAppService _service;

    public OrderController(IOrderAppService service) => _service = service;

    [HttpPost]
    public Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderInput input) => Run(() => _service.CreateAsync(input));

    [HttpGet]
    public Task<ActionResult<PagedResultDto<OrderListItemDto>>> GetMyOrders([FromQuery] GetMyOrdersInput input) => Run(() => _service.GetMyOrdersAsync(input));

    /// <summary>Chi tiết đơn: khách xem đơn của mình, Nhân viên/Quản trị viên xem mọi đơn.</summary>
    [HttpGet]
    public Task<ActionResult<OrderDto>> Get([FromQuery] int id) => Run(() => _service.GetAsync(id));

    [HttpPost]
    public Task<ActionResult<OrderDto>> Cancel([FromBody] EntityDto<int> input) => Run(() => _service.CancelAsync(input.Id));

    [HttpGet, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ActionResult<PagedResultDto<OrderListItemDto>>> GetAll([FromQuery] GetOrdersInput input) => Run(() => _service.GetAllAsync(input));

    [HttpPut, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ActionResult<OrderDto>> UpdateStatus([FromBody] UpdateOrderStatusInput input) => Run(() => _service.UpdateStatusAsync(input));
}

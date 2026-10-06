using Website.Api.Authorization;
using Website.Application.Common;
using Website.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Website.Api.Controllers.Shopping;

/// <summary>UC Đặt hàng, Quản lý đơn hàng cá nhân (khách hàng) và Quản lý đơn hàng (quản trị).</summary>
[Authorize]
public class OrderController : WebsiteBaseController
{
    private readonly IOrderAppService _service;

    public OrderController(IOrderAppService service) => _service = service;

    [HttpPost]
    public Task<OrderDto> Create([FromBody] CreateOrderInput input) => _service.CreateAsync(input);

    [HttpGet]
    public Task<PagedResultDto<OrderListItemDto>> GetMyOrders([FromQuery] GetMyOrdersInput input) => _service.GetMyOrdersAsync(input);

    /// <summary>Chi tiết đơn: khách xem đơn của mình, Nhân viên/Quản trị viên xem mọi đơn.</summary>
    [HttpGet]
    public Task<OrderDto> Get([FromQuery] int id) => _service.GetAsync(id);

    [HttpPost]
    public Task<OrderDto> Cancel([FromBody] EntityDto<int> input) => _service.CancelAsync(input.Id);

    [HttpGet, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<PagedResultDto<OrderListItemDto>> GetAll([FromQuery] GetOrdersInput input) => _service.GetAllAsync(input);

    [HttpPut, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<OrderDto> UpdateStatus([FromBody] UpdateOrderStatusInput input) => _service.UpdateStatusAsync(input);
}

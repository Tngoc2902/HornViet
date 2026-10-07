using Website.Api.Authorization;
using Website.Application.Common;
using Website.Application.Statistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Website.Api.Controllers.Admin;

/// <summary>UC Thống kê báo cáo (Quản trị viên) và trang tổng quan (Nhân viên, Quản trị viên).</summary>
[Authorize(Policy = AuthorizationPolicies.Management)]
public class StatisticController : WebsiteBaseController
{
    private readonly IStatisticAppService _service;

    public StatisticController(IStatisticAppService service) => _service = service;

    [HttpGet]
    public Task<ActionResult<DashboardDto>> GetDashboard() => Run(() => _service.GetDashboardAsync());

    [HttpGet, Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public Task<ActionResult<RevenueReportDto>> GetRevenue([FromQuery] GetRevenueInput input) => Run(() => _service.GetRevenueAsync(input));

    [HttpGet, Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public Task<ActionResult<ListResultDto<TopProductDto>>> GetTopProducts([FromQuery] GetTopProductsInput input) => Run(() => _service.GetTopProductsAsync(input));
}

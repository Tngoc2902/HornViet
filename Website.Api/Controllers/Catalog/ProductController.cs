using Website.Api.Authorization;
using Website.Application.Catalog;
using Website.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Website.Api.Controllers.Catalog;

/// <summary>UC Tìm kiếm & lọc, Xem chi tiết (công khai) và UC Quản lý sản phẩm & biến thể (quản trị).</summary>
public class ProductController : WebsiteBaseController
{
    private readonly IProductAppService _service;

    public ProductController(IProductAppService service) => _service = service;

    [HttpGet]
    public Task<ActionResult<PagedResultDto<ProductListItemDto>>> GetAll([FromQuery] GetProductsInput input) => Run(() => _service.GetAllAsync(input));

    [HttpGet]
    public Task<ActionResult<ProductDetailDto>> Get([FromQuery] int id) => Run(() => _service.GetAsync(id));

    [HttpGet, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ActionResult<PagedResultDto<ProductAdminListItemDto>>> GetAdminList([FromQuery] GetProductsForAdminInput input) =>
        Run(() => _service.GetAdminListAsync(input));

    [HttpPost, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ActionResult<ProductDetailDto>> Create([FromBody] CreateOrUpdateProductInput input) => Run(() => _service.CreateAsync(input));

    [HttpPut, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ActionResult<ProductDetailDto>> Update([FromBody] CreateOrUpdateProductInput input) => Run(() => _service.UpdateAsync(input));

    [HttpPut, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<IActionResult> SetActive([FromBody] SetProductActiveInput input) => Run(() => _service.SetActiveAsync(input));

    [HttpDelete, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<IActionResult> Delete([FromQuery] int id) => Run(() => _service.DeleteAsync(id));
}

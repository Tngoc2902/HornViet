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
    public Task<PagedResultDto<ProductListItemDto>> GetAll([FromQuery] GetProductsInput input) => _service.GetAllAsync(input);

    [HttpGet]
    public Task<ProductDetailDto> Get([FromQuery] int id) => _service.GetAsync(id);

    [HttpGet, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<PagedResultDto<ProductAdminListItemDto>> GetAdminList([FromQuery] GetProductsForAdminInput input) =>
        _service.GetAdminListAsync(input);

    [HttpPost, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ProductDetailDto> Create([FromBody] CreateOrUpdateProductInput input) => _service.CreateAsync(input);

    [HttpPut, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ProductDetailDto> Update([FromBody] CreateOrUpdateProductInput input) => _service.UpdateAsync(input);

    [HttpPut, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task SetActive([FromBody] SetProductActiveInput input) => _service.SetActiveAsync(input);

    [HttpDelete, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task Delete([FromQuery] int id) => _service.DeleteAsync(id);
}

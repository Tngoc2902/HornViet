using Website.Api.Authorization;
using Website.Application.Catalog;
using Website.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Website.Api.Controllers.Catalog;

/// <summary>UC Quản lý danh mục. Đọc: công khai; ghi: Nhân viên, Quản trị viên.</summary>
public class CategoryController : WebsiteBaseController
{
    private readonly ICategoryAppService _service;

    public CategoryController(ICategoryAppService service) => _service = service;

    [HttpGet]
    public Task<ListResultDto<CategoryDto>> GetAll() => _service.GetAllAsync();

    [HttpGet]
    public Task<CategoryDto> Get([FromQuery] int id) => _service.GetAsync(id);

    [HttpPost, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<CategoryDto> Create([FromBody] CreateOrUpdateCategoryInput input) => _service.CreateAsync(input);

    [HttpPut, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<CategoryDto> Update([FromBody] CreateOrUpdateCategoryInput input) => _service.UpdateAsync(input);

    [HttpDelete, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task Delete([FromQuery] int id) => _service.DeleteAsync(id);
}

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
    public Task<ActionResult<ListResultDto<CategoryDto>>> GetAll() => Run(() => _service.GetAllAsync());

    [HttpGet]
    public Task<ActionResult<CategoryDto>> Get([FromQuery] int id) => Run(() => _service.GetAsync(id));

    [HttpPost, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ActionResult<CategoryDto>> Create([FromBody] CreateOrUpdateCategoryInput input) => Run(() => _service.CreateAsync(input));

    [HttpPut, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ActionResult<CategoryDto>> Update([FromBody] CreateOrUpdateCategoryInput input) => Run(() => _service.UpdateAsync(input));

    [HttpDelete, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<IActionResult> Delete([FromQuery] int id) => Run(() => _service.DeleteAsync(id));
}

using Website.Api.Authorization;
using Website.Application.Catalog;
using Website.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Website.Api.Controllers.Catalog;

/// <summary>Nguyên liệu và nguồn gốc. Đọc: công khai (bộ lọc chất liệu); ghi: Nhân viên, Quản trị viên.</summary>
public class MaterialController : WebsiteBaseController
{
    private readonly IMaterialAppService _service;

    public MaterialController(IMaterialAppService service) => _service = service;

    [HttpGet]
    public Task<ActionResult<ListResultDto<MaterialDto>>> GetAll() => Run(() => _service.GetAllAsync());

    [HttpGet]
    public Task<ActionResult<MaterialDto>> Get([FromQuery] int id) => Run(() => _service.GetAsync(id));

    [HttpPost, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ActionResult<MaterialDto>> Create([FromBody] CreateOrUpdateMaterialInput input) => Run(() => _service.CreateAsync(input));

    [HttpPut, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ActionResult<MaterialDto>> Update([FromBody] CreateOrUpdateMaterialInput input) => Run(() => _service.UpdateAsync(input));

    [HttpDelete, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<IActionResult> Delete([FromQuery] int id) => Run(() => _service.DeleteAsync(id));
}

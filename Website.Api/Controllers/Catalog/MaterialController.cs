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
    public Task<ListResultDto<MaterialDto>> GetAll() => _service.GetAllAsync();

    [HttpGet]
    public Task<MaterialDto> Get([FromQuery] int id) => _service.GetAsync(id);

    [HttpPost, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<MaterialDto> Create([FromBody] CreateOrUpdateMaterialInput input) => _service.CreateAsync(input);

    [HttpPut, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<MaterialDto> Update([FromBody] CreateOrUpdateMaterialInput input) => _service.UpdateAsync(input);

    [HttpDelete, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task Delete([FromQuery] int id) => _service.DeleteAsync(id);
}

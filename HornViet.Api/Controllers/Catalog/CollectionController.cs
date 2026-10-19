using HornViet.Api.Authorization;
using HornViet.Application.Catalog;
using HornViet.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HornViet.Api.Controllers.Catalog;

/// <summary>Bộ sưu tập. Đọc: công khai (khách chỉ thấy bộ đang bật); ghi: Nhân viên, Quản trị viên.</summary>
public class CollectionController : HornVietBaseController
{
    private readonly ICollectionAppService _service;

    public CollectionController(ICollectionAppService service) => _service = service;

    [HttpGet]
    public Task<ActionResult<ListResultDto<CollectionDto>>> GetAll() => Run(() => _service.GetAllAsync());

    /// <summary>Theo mã (?id=) hoặc theo đường dẫn (?slug=).</summary>
    [HttpGet]
    public Task<ActionResult<CollectionDto>> Get([FromQuery] int? id, [FromQuery] string? slug) => Run(() => _service.GetAsync(id, slug));

    [HttpPost, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ActionResult<CollectionDto>> Create([FromBody] CreateOrUpdateCollectionInput input) => Run(() => _service.CreateAsync(input));

    [HttpPut, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<ActionResult<CollectionDto>> Update([FromBody] CreateOrUpdateCollectionInput input) => Run(() => _service.UpdateAsync(input));

    [HttpDelete, Authorize(Policy = AuthorizationPolicies.Management)]
    public Task<IActionResult> Delete([FromQuery] int id) => Run(() => _service.DeleteAsync(id));
}

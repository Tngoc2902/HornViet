using Website.Api.Authorization;
using Website.Application.Common;
using Website.Application.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Website.Api.Controllers.Admin;

/// <summary>UC Nhập kho &amp; giá vốn — chỉ Quản trị viên (giá nhập, giá vốn là thông tin nội bộ).</summary>
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class StockReceiptController : WebsiteBaseController
{
    private readonly IStockReceiptAppService _service;

    public StockReceiptController(IStockReceiptAppService service) => _service = service;

    /// <summary>Lịch sử phiếu nhập, mới nhất trước.</summary>
    [HttpGet]
    public Task<ActionResult<PagedResultDto<StockReceiptDto>>> GetAll([FromQuery] GetStockReceiptsInput input) => Run(() => _service.GetAllAsync(input));

    /// <summary>Tồn kho, giá vốn bình quân, giá trị tồn và lãi / sản phẩm của từng biến thể.</summary>
    [HttpGet]
    public Task<ActionResult<PagedResultDto<VariantCostDto>>> GetVariantCosts([FromQuery] GetVariantCostsInput input) =>
        Run(() => _service.GetVariantCostsAsync(input));

    [HttpPost]
    public Task<ActionResult<StockReceiptDto>> Create([FromBody] CreateStockReceiptInput input) => Run(() => _service.CreateAsync(input));

    /// <summary>Sửa phiếu nhập mới nhất của biến thể (tính lại giá vốn và lợi nhuận các đơn bán sau phiếu).</summary>
    [HttpPut]
    public Task<ActionResult<StockReceiptDto>> Update([FromBody] UpdateStockReceiptInput input) => Run(() => _service.UpdateAsync(input));

    /// <summary>Hủy phiếu nhập mới nhất của biến thể.</summary>
    [HttpDelete]
    public Task<IActionResult> Delete([FromQuery] int id) => Run(() => _service.DeleteAsync(id));
}

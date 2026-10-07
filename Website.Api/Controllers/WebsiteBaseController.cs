using Website.Api.Middlewares;
using Microsoft.AspNetCore.Mvc;

namespace Website.Api.Controllers;

/// <summary>
/// Controller gốc: route theo định dạng ABP <c>/api/services/app/{Service}/{Action}</c>.
/// Controller con khai báo <c>[Route]</c> riêng sẽ ghi đè route này.
/// </summary>
[ApiController]
[Route("api/services/app/[controller]/[action]")]
public abstract class WebsiteBaseController : ControllerBase
{
    // Mọi action gọi service qua Run(...): lỗi nghiệp vụ (UserFriendlyException, EntityNotFoundException,
    // DbUpdateConcurrencyException) được bắt NGAY TRONG code của dự án và đổi thành 400 / 404 / 409.
    // Nếu để lỗi lọt ra pipeline ASP.NET Core rồi mới bắt ở middleware, Visual Studio (F5, Just My Code) coi đó là
    // "Exception User-Unhandled" và dừng chương trình mỗi lần người dùng nhập sai — frontend phải chờ bấm Continue.
    // Response giữ nguyên định dạng AjaxResponse như middleware trả trước đây.

    /// <summary>Gọi service trả về dữ liệu; lỗi nghiệp vụ → AjaxResponse lỗi với HTTP status tương ứng.</summary>
    protected async Task<ActionResult<T>> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (BusinessErrors.TryMap(ex, out var status, out var response))
        {
            return BusinessError(ex, status, response);
        }
    }

    /// <summary>Gọi service không trả dữ liệu (result = null); lỗi nghiệp vụ xử lý như trên.</summary>
    protected async Task<IActionResult> Run(Func<Task> action)
    {
        try
        {
            await action();
            return Ok();
        }
        catch (Exception ex) when (BusinessErrors.TryMap(ex, out var status, out var response))
        {
            return BusinessError(ex, status, response);
        }
    }

    private ObjectResult BusinessError(Exception ex, int status, Models.AjaxResponse response)
    {
        HttpContext.RequestServices.GetRequiredService<ILogger<WebsiteBaseController>>()
            .LogInformation("Business error {Status} for {Path}: {Message}", status, Request.Path, ex.Message);
        return new ObjectResult(response) { StatusCode = status };
    }
}

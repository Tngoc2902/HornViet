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
}

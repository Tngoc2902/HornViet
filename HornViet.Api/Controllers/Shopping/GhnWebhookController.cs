using System.Security.Cryptography;
using System.Text;
using HornViet.Application.Shipping;
using HornViet.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HornViet.Api.Controllers.Shopping;

/// <summary>
/// Webhook Giao Hàng Nhanh gọi về khi trạng thái vận đơn đổi. GHN không ký payload nên xác thực bằng header tùy chỉnh
/// (Ghn:WebhookHeader = Ghn:WebhookToken) cấu hình ở cổng GHN. Mọi lỗi nghiệp vụ đều trả 200 để GHN không gửi lại mãi.
/// </summary>
[Route("api/shipping/ghn-webhook")]
public class GhnWebhookController : HornVietBaseController
{
    private readonly IShipmentAppService _service;
    private readonly GhnOptions _options;
    private readonly ILogger<GhnWebhookController> _logger;

    public GhnWebhookController(IShipmentAppService service, IOptions<GhnOptions> options, ILogger<GhnWebhookController> logger)
    {
        _service = service;
        _options = options.Value;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] GhnCallbackInput input)
    {
        if (!IsAuthorized())
            return Unauthorized();

        try
        {
            await _service.HandleCallbackAsync(input);
        }
        catch (Exception ex)
        {
            // Lỗi tạm thời (DB…) để GHN gửi lại là hợp lý; còn lỗi nghiệp vụ đã được xử lý bên trong service.
            _logger.LogError(ex, "Xử lý webhook GHN lỗi cho vận đơn {Code}", input.OrderCode);
            return StatusCode(500);
        }
        return Ok();
    }

    private bool IsAuthorized()
    {
        if (string.IsNullOrEmpty(_options.WebhookToken))
            return false; // chưa cấu hình khóa → từ chối tất cả
        var supplied = Request.Headers[_options.WebhookHeader].ToString();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(_options.WebhookToken));
    }
}

using HornViet.Application.Chat;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HornViet.Api.Controllers.Chat;

/// <summary>
/// Ô chat ở trang khách — công khai (khách vãng lai dùng được). Khóa khách (guestKey) luôn gửi trong thân request,
/// không đặt trên URL. Gửi tin / gọi nhân viên bị giới hạn tần suất để chống spam.
/// </summary>
public class ChatController : HornVietBaseController
{
    /// <summary>Tên chính sách giới hạn tần suất (đăng ký ở Program.cs).</summary>
    public const string RateLimitPolicy = "chat";

    private readonly IChatbotAppService _service;

    public ChatController(IChatbotAppService service) => _service = service;

    /// <summary>Hội thoại đang mở (result = null nếu chưa có).</summary>
    [HttpPost]
    public Task<ActionResult<ChatConversationDto?>> Get([FromBody] ChatAccessInput input) => Run(() => _service.GetAsync(input));

    [HttpPost, EnableRateLimiting(RateLimitPolicy)]
    public Task<ActionResult<ChatSendResultDto>> Send([FromBody] SendChatInput input) => Run(() => _service.SendAsync(input));

    [HttpPost, EnableRateLimiting(RateLimitPolicy)]
    public Task<ActionResult<ChatConversationDto>> RequestStaff([FromBody] ChatAccessInput input) => Run(() => _service.RequestStaffAsync(input));

    [HttpPost]
    public Task<ActionResult<ChatConversationDto>> CancelStaff([FromBody] ChatAccessInput input) => Run(() => _service.CancelStaffAsync(input));

    [HttpPost]
    public Task<IActionResult> Close([FromBody] ChatAccessInput input) => Run(() => _service.CloseAsync(input));
}

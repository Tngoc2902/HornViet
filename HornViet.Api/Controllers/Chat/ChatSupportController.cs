using HornViet.Api.Authorization;
using HornViet.Application.Chat;
using HornViet.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HornViet.Api.Controllers.Chat;

/// <summary>Hỗ trợ khách hàng (chat trực tiếp) — Nhân viên, Quản trị viên.</summary>
[Authorize(Policy = AuthorizationPolicies.Management)]
public class ChatSupportController : HornVietBaseController
{
    private readonly IChatSupportAppService _service;

    public ChatSupportController(IChatSupportAppService service) => _service = service;

    [HttpGet]
    public Task<ActionResult<PagedResultDto<ChatConversationListItemDto>>> GetConversations([FromQuery] GetChatConversationsInput input) =>
        Run(() => _service.GetConversationsAsync(input));

    [HttpGet]
    public Task<ActionResult<ChatSupportConversationDto>> GetConversation([FromQuery] int id) => Run(() => _service.GetConversationAsync(id));

    [HttpPost]
    public Task<ActionResult<ChatSupportConversationDto>> Claim([FromBody] ClaimChatInput input) => Run(() => _service.ClaimAsync(input));

    [HttpPost]
    public Task<ActionResult<ChatMessageDto>> Reply([FromBody] StaffReplyInput input) => Run(() => _service.ReplyAsync(input));

    [HttpPost]
    public Task<ActionResult<ChatSupportConversationDto>> Close([FromBody] EntityDto<int> input) => Run(() => _service.CloseAsync(input.Id));

    [HttpGet]
    public Task<ActionResult<ChatSupportSummaryDto>> GetSummary() => Run(() => _service.GetSummaryAsync());
}

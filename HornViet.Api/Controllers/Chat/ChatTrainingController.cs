using HornViet.Api.Authorization;
using HornViet.Application.Chat;
using HornViet.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HornViet.Api.Controllers.Chat;

/// <summary>Huấn luyện chatbot: ý định, câu mẫu, huấn luyện model, câu bot chưa hiểu — Nhân viên, Quản trị viên.</summary>
[Authorize(Policy = AuthorizationPolicies.Management)]
public class ChatTrainingController : HornVietBaseController
{
    private readonly IChatTrainingAppService _service;

    public ChatTrainingController(IChatTrainingAppService service) => _service = service;

    [HttpGet]
    public Task<ActionResult<ListResultDto<ChatIntentDto>>> GetIntents() => Run(() => _service.GetIntentsAsync());

    [HttpGet]
    public Task<ActionResult<ChatIntentDto>> GetIntent([FromQuery] int id) => Run(() => _service.GetIntentAsync(id));

    [HttpPost]
    public Task<ActionResult<ChatIntentDto>> CreateIntent([FromBody] CreateOrUpdateChatIntentInput input) => Run(() => _service.CreateIntentAsync(input));

    [HttpPut]
    public Task<ActionResult<ChatIntentDto>> UpdateIntent([FromBody] CreateOrUpdateChatIntentInput input) => Run(() => _service.UpdateIntentAsync(input));

    [HttpDelete]
    public Task<IActionResult> DeleteIntent([FromQuery] int id) => Run(() => _service.DeleteIntentAsync(id));

    [HttpPost]
    public Task<ActionResult<ChatModelDto>> Train() => Run(() => _service.TrainAsync());

    /// <summary>Model đang dùng (result = null nếu chưa huấn luyện).</summary>
    [HttpGet]
    public Task<ActionResult<ChatModelDto?>> GetModel() => Run(() => _service.GetModelAsync());

    [HttpPost]
    public Task<ActionResult<ChatTestResultDto>> Test([FromBody] TestChatInput input) => Run(() => _service.TestAsync(input));

    [HttpGet]
    public Task<ActionResult<PagedResultDto<ChatUnrecognizedDto>>> GetUnrecognized([FromQuery] PagedInput input) =>
        Run(() => _service.GetUnrecognizedAsync(input));

    [HttpPost]
    public Task<IActionResult> Label([FromBody] LabelChatMessageInput input) => Run(() => _service.LabelAsync(input));

    [HttpPost]
    public Task<IActionResult> Dismiss([FromBody] EntityDto<long> input) => Run(() => _service.DismissAsync(input.Id));
}

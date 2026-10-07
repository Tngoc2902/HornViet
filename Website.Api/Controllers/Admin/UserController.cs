using Website.Api.Authorization;
using Website.Application.Common;
using Website.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Website.Api.Controllers.Admin;

/// <summary>UC Quản lý người dùng — chỉ Quản trị viên.</summary>
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class UserController : WebsiteBaseController
{
    private readonly IUserAppService _service;

    public UserController(IUserAppService service) => _service = service;

    [HttpGet]
    public Task<ActionResult<PagedResultDto<UserDto>>> GetAll([FromQuery] GetUsersInput input) => Run(() => _service.GetAllAsync(input));

    [HttpGet]
    public Task<ActionResult<UserDto>> Get([FromQuery] long id) => Run(() => _service.GetAsync(id));

    [HttpGet]
    public Task<ActionResult<ListResultDto<RoleDto>>> GetRoles() => Run(() => _service.GetRolesAsync());

    [HttpPost]
    public Task<ActionResult<UserDto>> Create([FromBody] CreateUserInput input) => Run(() => _service.CreateAsync(input));

    [HttpPut]
    public Task<ActionResult<UserDto>> SetRoles([FromBody] SetUserRolesInput input) => Run(() => _service.SetRolesAsync(input));

    /// <summary>Khóa (isActive = false) / mở khóa (isActive = true) tài khoản.</summary>
    [HttpPut]
    public Task<ActionResult<UserDto>> SetActive([FromBody] SetUserActiveInput input) => Run(() => _service.SetActiveAsync(input));

    /// <summary>Đặt lại mật khẩu cho tài khoản khác.</summary>
    [HttpPut]
    public Task<IActionResult> ResetPassword([FromBody] ResetUserPasswordInput input) => Run(() => _service.ResetPasswordAsync(input));
}

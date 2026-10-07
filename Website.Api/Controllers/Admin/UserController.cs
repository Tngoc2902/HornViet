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
    public Task<PagedResultDto<UserDto>> GetAll([FromQuery] GetUsersInput input) => _service.GetAllAsync(input);

    [HttpGet]
    public Task<UserDto> Get([FromQuery] long id) => _service.GetAsync(id);

    [HttpGet]
    public Task<ListResultDto<RoleDto>> GetRoles() => _service.GetRolesAsync();

    [HttpPost]
    public Task<UserDto> Create([FromBody] CreateUserInput input) => _service.CreateAsync(input);

    [HttpPut]
    public Task<UserDto> SetRoles([FromBody] SetUserRolesInput input) => _service.SetRolesAsync(input);

    /// <summary>Khóa (isActive = false) / mở khóa (isActive = true) tài khoản.</summary>
    [HttpPut]
    public Task<UserDto> SetActive([FromBody] SetUserActiveInput input) => _service.SetActiveAsync(input);

    /// <summary>Đặt lại mật khẩu cho tài khoản khác.</summary>
    [HttpPut]
    public Task ResetPassword([FromBody] ResetUserPasswordInput input) => _service.ResetPasswordAsync(input);
}

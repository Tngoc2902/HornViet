using Website.Api.Authorization;
using Website.Api.Models.Account;
using Website.Application.Accounts;
using Website.Domain.SharedKernel;
using Website.Domain.Users.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Website.Api.Controllers.Account;

/// <summary>UC Đăng nhập. Đăng xuất: frontend chỉ cần xóa accessToken (JWT không lưu trạng thái phía server).</summary>
[Route("api/[controller]/[action]")]
public class TokenAuthController : WebsiteBaseController
{
    private readonly IAccountAppService _accountAppService;
    private readonly TokenService _tokenService;

    public TokenAuthController(IAccountAppService accountAppService, TokenService tokenService)
    {
        _accountAppService = accountAppService;
        _tokenService = tokenService;
    }

    /// <summary>Đăng nhập cửa hàng (HornViet_customer): cấp token cho mọi vai trò.</summary>
    [HttpPost]
    public async Task<AuthenticateResultModel> Authenticate([FromBody] AuthenticateModel model)
    {
        var user = await _accountAppService.ValidateCredentialsAsync(model);
        return CreateResult(user);
    }

    /// <summary>
    /// Đăng nhập trang quản trị (HornViet_admin): chỉ cấp token cho Nhân viên / Quản trị viên.
    /// Tài khoản Khách hàng nhận 400 và không có token.
    /// </summary>
    [HttpPost]
    public async Task<AuthenticateResultModel> AuthenticateAdmin([FromBody] AuthenticateModel model)
    {
        var user = await _accountAppService.ValidateCredentialsAsync(model);
        if (!user.Roles.Contains(StaticRoleNames.Admin) && !user.Roles.Contains(StaticRoleNames.Staff))
            throw new UserFriendlyException("Tài khoản không có quyền truy cập trang quản trị.");

        return CreateResult(user);
    }

    private AuthenticateResultModel CreateResult(AuthenticatedUser user)
    {
        var (token, expireInSeconds) = _tokenService.CreateToken(user);
        return new AuthenticateResultModel
        {
            AccessToken = token,
            ExpireInSeconds = expireInSeconds,
            UserId = user.UserId,
            UserName = user.UserName,
            Name = user.Name,
            Roles = user.Roles
        };
    }
}

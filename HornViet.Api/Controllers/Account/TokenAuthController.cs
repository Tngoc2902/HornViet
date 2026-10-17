using HornViet.Api.Authorization;
using HornViet.Api.Models.Account;
using HornViet.Application.Accounts;
using HornViet.Domain.SharedKernel;
using HornViet.Domain.Users.Enums;
using Microsoft.AspNetCore.Mvc;

namespace HornViet.Api.Controllers.Account;

/// <summary>UC Đăng nhập. Đăng xuất: frontend chỉ cần xóa accessToken (JWT không lưu trạng thái phía server).</summary>
[Route("api/[controller]/[action]")]
public class TokenAuthController : HornVietBaseController
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
    public Task<ActionResult<AuthenticateResultModel>> Authenticate([FromBody] AuthenticateModel model) =>
        Run(async () => CreateResult(await _accountAppService.ValidateCredentialsAsync(model)));

    /// <summary>
    /// Đăng nhập cửa hàng bằng Google: nhận access token Google cấp cho trang cửa hàng, chưa có tài khoản thì tạo
    /// tài khoản Khách hàng. Trả về cùng dạng token như đăng nhập bằng mật khẩu.
    /// </summary>
    [HttpPost]
    public Task<ActionResult<AuthenticateResultModel>> AuthenticateGoogle([FromBody] GoogleLoginInput input) =>
        Run(async () => CreateResult(await _accountAppService.AuthenticateWithGoogleAsync(input)));

    /// <summary>
    /// Đăng nhập trang quản trị (HornViet_admin): chỉ cấp token cho Nhân viên / Quản trị viên.
    /// Tài khoản Khách hàng nhận 400 và không có token.
    /// </summary>
    [HttpPost]
    public Task<ActionResult<AuthenticateResultModel>> AuthenticateAdmin([FromBody] AuthenticateModel model) =>
        Run(async () =>
        {
            var user = await _accountAppService.ValidateCredentialsAsync(model);
            if (!user.Roles.Contains(nameof(RoleName.Admin)) && !user.Roles.Contains(nameof(RoleName.Staff)))
                throw new UserFriendlyException("Tài khoản không có quyền truy cập trang quản trị.");

            return CreateResult(user);
        });

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

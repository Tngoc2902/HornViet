using Website.Application.Accounts;
using Microsoft.AspNetCore.Mvc;

namespace Website.Api.Controllers.Account;

/// <summary>UC Đăng ký và UC Quên mật khẩu (công khai, không cần đăng nhập).</summary>
public class AccountController : WebsiteBaseController
{
    private readonly IAccountAppService _accountAppService;

    public AccountController(IAccountAppService accountAppService) => _accountAppService = accountAppService;

    [HttpPost]
    public Task<RegisterOutput> Register([FromBody] RegisterInput input) => _accountAppService.RegisterAsync(input);

    /// <summary>Gửi email chứa link đặt lại mật khẩu. Luôn trả thành công để không lộ email nào đã đăng ký.</summary>
    [HttpPost]
    public Task ForgotPassword([FromBody] ForgotPasswordInput input) => _accountAppService.ForgotPasswordAsync(input);

    /// <summary>Đặt mật khẩu mới bằng mã trong link email (dùng một lần, hết hạn sau 30 phút).</summary>
    [HttpPost]
    public Task ResetPassword([FromBody] ResetPasswordInput input) => _accountAppService.ResetPasswordAsync(input);
}

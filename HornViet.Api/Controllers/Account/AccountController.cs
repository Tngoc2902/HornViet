using HornViet.Application.Accounts;
using Microsoft.AspNetCore.Mvc;

namespace HornViet.Api.Controllers.Account;

/// <summary>UC Đăng ký và UC Quên mật khẩu (công khai, không cần đăng nhập).</summary>
public class AccountController : HornVietBaseController
{
    private readonly IAccountAppService _accountAppService;

    public AccountController(IAccountAppService accountAppService) => _accountAppService = accountAppService;

    [HttpPost]
    public Task<ActionResult<RegisterOutput>> Register([FromBody] RegisterInput input) => Run(() => _accountAppService.RegisterAsync(input));

    /// <summary>Quên mật khẩu, bước 1: gửi email chứa mã xác nhận 6 số (hiệu lực 60 giây). Luôn trả thành công để không lộ email nào đã đăng ký.</summary>
    [HttpPost]
    public Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordInput input) => Run(() => _accountAppService.ForgotPasswordAsync(input));

    /// <summary>Bước 2: kiểm tra mã xác nhận (tối đa 5 lần nhập sai); đúng thì trả "vé" dùng một lần để đặt mật khẩu mới.</summary>
    [HttpPost]
    public Task<ActionResult<VerifyResetCodeOutput>> VerifyResetCode([FromBody] VerifyResetCodeInput input) => Run(() => _accountAppService.VerifyResetCodeAsync(input));

    /// <summary>Bước 3: đặt mật khẩu mới bằng vé nhận được ở bước 2 (dùng một lần, hết hạn sau 10 phút).</summary>
    [HttpPost]
    public Task<IActionResult> ResetPassword([FromBody] ResetPasswordInput input) => Run(() => _accountAppService.ResetPasswordAsync(input));
}

using Website.Application.Accounts;
using Microsoft.AspNetCore.Mvc;

namespace Website.Api.Controllers.Account;

/// <summary>UC Đăng ký.</summary>
public class AccountController : WebsiteBaseController
{
    private readonly IAccountAppService _accountAppService;

    public AccountController(IAccountAppService accountAppService) => _accountAppService = accountAppService;

    [HttpPost]
    public Task<RegisterOutput> Register([FromBody] RegisterInput input) => _accountAppService.RegisterAsync(input);
}

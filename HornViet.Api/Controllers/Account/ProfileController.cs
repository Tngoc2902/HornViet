using HornViet.Application.Common;
using HornViet.Application.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HornViet.Api.Controllers.Account;

/// <summary>UC Quản lý hồ sơ cá nhân (thông tin, đổi mật khẩu, sổ địa chỉ).</summary>
[Authorize]
public class ProfileController : HornVietBaseController
{
    private readonly IProfileAppService _profileAppService;

    public ProfileController(IProfileAppService profileAppService) => _profileAppService = profileAppService;

    [HttpGet]
    public Task<ActionResult<ProfileDto>> GetMyProfile() => Run(() => _profileAppService.GetMyProfileAsync());

    [HttpPut]
    public Task<ActionResult<ProfileDto>> UpdateMyProfile([FromBody] UpdateProfileInput input) => Run(() => _profileAppService.UpdateMyProfileAsync(input));

    [HttpPost]
    public Task<IActionResult> ChangePassword([FromBody] ChangePasswordInput input) => Run(() => _profileAppService.ChangePasswordAsync(input));

    [HttpGet]
    public Task<ActionResult<ListResultDto<AddressDto>>> GetMyAddresses() => Run(() => _profileAppService.GetMyAddressesAsync());

    /// <summary>Tạo mới (không có id) hoặc cập nhật (có id) địa chỉ giao hàng.</summary>
    [HttpPost]
    public Task<ActionResult<AddressDto>> SaveAddress([FromBody] CreateOrUpdateAddressInput input) => Run(() => _profileAppService.SaveAddressAsync(input));

    [HttpDelete]
    public Task<IActionResult> DeleteAddress([FromQuery] int id) => Run(() => _profileAppService.DeleteAddressAsync(id));
}

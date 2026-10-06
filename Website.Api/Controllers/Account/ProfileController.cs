using Website.Application.Common;
using Website.Application.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Website.Api.Controllers.Account;

/// <summary>UC Quản lý hồ sơ cá nhân (thông tin, đổi mật khẩu, sổ địa chỉ).</summary>
[Authorize]
public class ProfileController : WebsiteBaseController
{
    private readonly IProfileAppService _profileAppService;

    public ProfileController(IProfileAppService profileAppService) => _profileAppService = profileAppService;

    [HttpGet]
    public Task<ProfileDto> GetMyProfile() => _profileAppService.GetMyProfileAsync();

    [HttpPut]
    public Task<ProfileDto> UpdateMyProfile([FromBody] UpdateProfileInput input) => _profileAppService.UpdateMyProfileAsync(input);

    [HttpPost]
    public Task ChangePassword([FromBody] ChangePasswordInput input) => _profileAppService.ChangePasswordAsync(input);

    [HttpGet]
    public Task<ListResultDto<AddressDto>> GetMyAddresses() => _profileAppService.GetMyAddressesAsync();

    /// <summary>Tạo mới (không có id) hoặc cập nhật (có id) địa chỉ giao hàng.</summary>
    [HttpPost]
    public Task<AddressDto> SaveAddress([FromBody] CreateOrUpdateAddressInput input) => _profileAppService.SaveAddressAsync(input);

    [HttpDelete]
    public Task DeleteAddress([FromQuery] int id) => _profileAppService.DeleteAddressAsync(id);
}

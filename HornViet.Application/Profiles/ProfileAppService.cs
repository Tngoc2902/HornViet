using System.ComponentModel.DataAnnotations;
using HornViet.Application.Accounts;
using HornViet.Application.Common;
using HornViet.Application.Shipping;
using HornViet.Domain;
using HornViet.Domain.Interfaces.Repositories;
using HornViet.Domain.SharedKernel;
using HornViet.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HornViet.Application.Profiles;

public class ProfileDto
{
    public long Id { get; set; }
    public string UserName { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string EmailAddress { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public IReadOnlyList<string> RoleNames { get; set; } = Array.Empty<string>();
    /// <summary>false = tài khoản tạo bằng Google, chưa đặt mật khẩu (tab Đổi mật khẩu hướng dẫn tạo qua Quên mật khẩu).</summary>
    public bool HasPassword { get; set; }
    /// <summary>Đã liên kết tài khoản Google (đăng nhập được bằng nút Google).</summary>
    public bool HasGoogleLogin { get; set; }
}

public class UpdateProfileInput
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên."), StringLength(64)]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(AppConsts.PhoneRegex, ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string PhoneNumber { get; set; } = null!;
}

public class ChangePasswordInput
{
    [Required] public string CurrentPassword { get; set; } = null!;
    [Required] public string NewPassword { get; set; } = null!;
}

public class AddressDto
{
    public int Id { get; set; }
    public string ReceiverName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string Address { get; set; } = null!;
    public bool IsDefault { get; set; }

    /// <summary>Mã tỉnh / phường và số nhà — dùng để đặt hàng có giao hàng nhanh; null ở địa chỉ cũ.</summary>
    public int? ProvinceId { get; set; }
    public string? WardCode { get; set; }
    public string? Street { get; set; }

    /// <summary>Đã bật giao hàng nhanh mà địa chỉ chưa có tỉnh / phường: khách cần sửa lại trước khi dùng để đặt hàng.</summary>
    public bool NeedsUpdate { get; set; }
}

public class CreateOrUpdateAddressInput
{
    /// <summary>Bỏ trống khi tạo mới.</summary>
    public int? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên người nhận."), StringLength(255)]
    public string ReceiverName { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(AppConsts.PhoneRegex, ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string PhoneNumber { get; set; } = null!;

    /// <summary>Khi bật giao hàng nhanh: số nhà, tên đường (server ghép thêm phường / tỉnh). Khi tắt: địa chỉ đầy đủ.</summary>
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ."), StringLength(300)]
    public string Address { get; set; } = null!;

    /// <summary>Tỉnh / phường (danh mục mới 2 cấp) — bắt buộc khi đã bật giao hàng nhanh.</summary>
    public int? ProvinceId { get; set; }
    public string? WardCode { get; set; }

    public bool IsDefault { get; set; }
}

public interface IProfileAppService
{
    Task<ProfileDto> GetMyProfileAsync();
    Task<ProfileDto> UpdateMyProfileAsync(UpdateProfileInput input);
    Task ChangePasswordAsync(ChangePasswordInput input);
    Task<ListResultDto<AddressDto>> GetMyAddressesAsync();
    Task<AddressDto> SaveAddressAsync(CreateOrUpdateAddressInput input);
    Task DeleteAddressAsync(int id);
}

public class ProfileAppService : IProfileAppService
{
    private readonly IRepository<User, long> _userRepository;
    private readonly IRepository<UserAddress, int> _addressRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IAppSession _session;
    private readonly IRealtimeNotifier _notifier;
    private readonly IShippingAppService _shipping;

    public ProfileAppService(IRepository<User, long> userRepository, IRepository<UserAddress, int> addressRepository,
        IUnitOfWork unitOfWork, IPasswordHasher<User> passwordHasher, IAppSession session, IRealtimeNotifier notifier,
        IShippingAppService shipping)
    {
        _shipping = shipping;
        _userRepository = userRepository;
        _addressRepository = addressRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _session = session;
        _notifier = notifier;
    }

    public async Task<ProfileDto> GetMyProfileAsync() => ToDto(await GetCurrentUserAsync());

    public async Task<ProfileDto> UpdateMyProfileAsync(UpdateProfileInput input)
    {
        // Email là định danh đăng nhập nên không cho tự thay đổi ở đây.
        var user = await GetCurrentUserAsync();
        user.Name = input.Name.Trim();
        user.PhoneNumber = input.PhoneNumber.Trim();
        await _unitOfWork.SaveChangesAsync();
        return ToDto(user);
    }

    public async Task ChangePasswordAsync(ChangePasswordInput input)
    {
        var user = await GetCurrentUserAsync();
        // Tài khoản tạo bằng Google: tạo mật khẩu qua Quên mật khẩu (xác nhận bằng email) chứ không đặt thẳng ở đây.
        if (!user.HasPassword)
            throw new UserFriendlyException("Tài khoản chưa có mật khẩu. Vui lòng dùng “Quên mật khẩu” để tạo mật khẩu.");
        if (_passwordHasher.VerifyHashedPassword(user, user.Password, input.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new UserFriendlyException("Mật khẩu hiện tại không đúng.");

        PasswordPolicy.Validate(input.NewPassword);
        user.Password = _passwordHasher.HashPassword(user, input.NewPassword);
        user.RotateSecurityStamp(); // đăng xuất mọi phiên (kể cả phiên hiện tại) — frontend yêu cầu đăng nhập lại
        await _unitOfWork.SaveChangesAsync();
        await _notifier.SessionRevokedAsync(user.Id, SessionRevokedReason.PasswordChanged); // các tab / thiết bị khác
    }

    public async Task<ListResultDto<AddressDto>> GetMyAddressesAsync()
    {
        var userId = _session.GetUserId();
        var items = await _addressRepository.GetAll()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.CreationTime)
            .ToListAsync();
        return new ListResultDto<AddressDto>(items.Select(ToDto).ToList());
    }

    public async Task<AddressDto> SaveAddressAsync(CreateOrUpdateAddressInput input)
    {
        var userId = _session.GetUserId();
        var myAddresses = await _addressRepository.GetAll().Where(a => a.UserId == userId).ToListAsync();

        UserAddress address;
        if (input.Id.HasValue)
        {
            address = myAddresses.FirstOrDefault(a => a.Id == input.Id.Value)
                      ?? throw new EntityNotFoundException("Không tìm thấy địa chỉ.");
        }
        else
        {
            address = new UserAddress { UserId = userId };
            await _addressRepository.InsertAsync(address);
            myAddresses.Add(address);
        }

        address.ReceiverName = input.ReceiverName.Trim();
        address.PhoneNumber = input.PhoneNumber.Trim();
        var street = input.Address.Trim();
        if (_shipping.IsEnabled)
        {
            // Lưu kèm mã tỉnh / phường để đặt hàng tính phí và tạo vận đơn được; tên lấy từ danh mục chuẩn.
            if (input.ProvinceId is not > 0 || string.IsNullOrWhiteSpace(input.WardCode))
                throw new UserFriendlyException("Vui lòng chọn tỉnh / thành phố và phường / xã.");
            var (provinceName, wardName) = await _shipping.ResolveNamesAsync(input.ProvinceId.Value, input.WardCode);
            address.ProvinceId = input.ProvinceId;
            address.WardCode = input.WardCode.Trim();
            address.Street = street;
            address.Address = $"{street}, {wardName}, {provinceName}";
        }
        else
        {
            address.ProvinceId = null;
            address.WardCode = null;
            address.Street = null;
            address.Address = street;
        }

        // Luôn có đúng một địa chỉ mặc định.
        if (input.IsDefault || myAddresses.All(a => !a.IsDefault || a == address))
        {
            foreach (var a in myAddresses) a.IsDefault = false;
            address.IsDefault = true;
        }

        await _unitOfWork.SaveChangesAsync();
        return ToDto(address);
    }

    private AddressDto ToDto(UserAddress a) => new()
    {
        Id = a.Id, ReceiverName = a.ReceiverName, PhoneNumber = a.PhoneNumber,
        Address = a.Address, IsDefault = a.IsDefault,
        ProvinceId = a.ProvinceId, WardCode = a.WardCode, Street = a.Street,
        NeedsUpdate = _shipping.IsEnabled && (a.ProvinceId == null || string.IsNullOrEmpty(a.WardCode))
    };

    public async Task DeleteAddressAsync(int id)
    {
        var userId = _session.GetUserId();
        var address = await _addressRepository.GetAll().FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId)
                      ?? throw new EntityNotFoundException("Không tìm thấy địa chỉ.");
        _addressRepository.Delete(address);

        if (address.IsDefault)
        {
            var next = await _addressRepository.GetAll()
                .Where(a => a.UserId == userId && a.Id != id)
                .OrderByDescending(a => a.CreationTime).FirstOrDefaultAsync();
            if (next != null) next.IsDefault = true;
        }
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<User> GetCurrentUserAsync()
    {
        var userId = _session.GetUserId();
        return await _userRepository.GetAll()
                   .Include(u => u.Roles).ThenInclude(r => r.Role)
                   .FirstOrDefaultAsync(u => u.Id == userId)
               ?? throw new EntityNotFoundException("Không tìm thấy tài khoản.");
    }

    private static ProfileDto ToDto(User user) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        Name = user.Name,
        EmailAddress = user.EmailAddress,
        PhoneNumber = user.PhoneNumber,
        RoleNames = user.Roles.Where(r => r.Role != null).Select(r => r.Role.Name).ToList(),
        HasPassword = user.HasPassword,
        HasGoogleLogin = user.GoogleId != null
    };
}

using System.ComponentModel.DataAnnotations;
using Website.Application.Accounts;
using Website.Application.Common;
using Website.Domain;
using Website.Domain.Interfaces.Repositories;
using Website.Domain.SharedKernel;
using Website.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Website.Application.Profiles;

public class ProfileDto
{
    public long Id { get; set; }
    public string UserName { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string EmailAddress { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public IReadOnlyList<string> RoleNames { get; set; } = Array.Empty<string>();
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

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ."), StringLength(500)]
    public string Address { get; set; } = null!;

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

    public ProfileAppService(IRepository<User, long> userRepository, IRepository<UserAddress, int> addressRepository,
        IUnitOfWork unitOfWork, IPasswordHasher<User> passwordHasher, IAppSession session)
    {
        _userRepository = userRepository;
        _addressRepository = addressRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _session = session;
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
        if (_passwordHasher.VerifyHashedPassword(user, user.Password, input.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new UserFriendlyException("Mật khẩu hiện tại không đúng.");

        PasswordPolicy.Validate(input.NewPassword);
        user.Password = _passwordHasher.HashPassword(user, input.NewPassword);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<ListResultDto<AddressDto>> GetMyAddressesAsync()
    {
        var userId = _session.GetUserId();
        var items = await _addressRepository.GetAll()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.CreationTime)
            .Select(a => new AddressDto
            {
                Id = a.Id, ReceiverName = a.ReceiverName, PhoneNumber = a.PhoneNumber,
                Address = a.Address, IsDefault = a.IsDefault
            })
            .ToListAsync();
        return new ListResultDto<AddressDto>(items);
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
        address.Address = input.Address.Trim();

        // Luôn có đúng một địa chỉ mặc định.
        if (input.IsDefault || myAddresses.All(a => !a.IsDefault || a == address))
        {
            foreach (var a in myAddresses) a.IsDefault = false;
            address.IsDefault = true;
        }

        await _unitOfWork.SaveChangesAsync();
        return new AddressDto
        {
            Id = address.Id, ReceiverName = address.ReceiverName, PhoneNumber = address.PhoneNumber,
            Address = address.Address, IsDefault = address.IsDefault
        };
    }

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
        RoleNames = user.Roles.Where(r => r.Role != null).Select(r => r.Role.Name).ToList()
    };
}

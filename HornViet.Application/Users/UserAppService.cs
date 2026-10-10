using System.ComponentModel.DataAnnotations;
using HornViet.Application.Accounts;
using HornViet.Application.Common;
using HornViet.Domain;
using HornViet.Domain.Interfaces.Repositories;
using HornViet.Domain.SharedKernel;
using HornViet.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HornViet.Domain.Users.Enums;

namespace HornViet.Application.Users;

public class UserDto
{
    public long Id { get; set; }
    public string UserName { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string EmailAddress { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreationTime { get; set; }
    public IReadOnlyList<string> RoleNames { get; set; } = Array.Empty<string>();
}

public class RoleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
}

public class GetUsersInput : PagedInput
{
    /// <summary>Tìm theo tên đăng nhập, họ tên, email hoặc số điện thoại.</summary>
    public string? Keyword { get; set; }
    public string? RoleName { get; set; }
    public bool? IsActive { get; set; }
}

public class CreateUserInput
{
    [Required, StringLength(64)]
    public string Name { get; set; } = null!;

    [Required, EmailAddress, StringLength(256)]
    public string EmailAddress { get; set; } = null!;

    [StringLength(256)]
    public string? UserName { get; set; }

    [RegularExpression(AppConsts.PhoneRegex, ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string? PhoneNumber { get; set; }

    [Required]
    public string Password { get; set; } = null!;

    /// <summary>VD: ["Staff"]</summary>
    [MinLength(1, ErrorMessage = "Chọn ít nhất một vai trò.")]
    public List<string> RoleNames { get; set; } = new();
}

public class SetUserRolesInput
{
    public long UserId { get; set; }

    [MinLength(1, ErrorMessage = "Chọn ít nhất một vai trò.")]
    public List<string> RoleNames { get; set; } = new();
}

public class SetUserActiveInput
{
    public long UserId { get; set; }
    public bool IsActive { get; set; }
}

public class ResetUserPasswordInput
{
    public long UserId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    public string NewPassword { get; set; } = null!;
}

public interface IUserAppService
{
    Task<PagedResultDto<UserDto>> GetAllAsync(GetUsersInput input);
    Task<UserDto> GetAsync(long id);
    Task<ListResultDto<RoleDto>> GetRolesAsync();
    Task<UserDto> CreateAsync(CreateUserInput input);
    Task<UserDto> SetRolesAsync(SetUserRolesInput input);
    Task<UserDto> SetActiveAsync(SetUserActiveInput input);
    Task ResetPasswordAsync(ResetUserPasswordInput input);
}

public class UserAppService : IUserAppService
{
    private readonly IRepository<User, long> _userRepository;
    private readonly IRepository<Role, int> _roleRepository;
    private readonly IRepository<UserRole, long> _userRoleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IAppSession _session;

    public UserAppService(IRepository<User, long> userRepository, IRepository<Role, int> roleRepository,
        IRepository<UserRole, long> userRoleRepository, IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher, IAppSession session)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _session = session;
    }

    public async Task<PagedResultDto<UserDto>> GetAllAsync(GetUsersInput input)
    {
        var query = _userRepository.GetAll();
        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var k = input.Keyword.Trim();
            query = query.Where(u => u.UserName.Contains(k) || u.Name.Contains(k) ||
                                     u.EmailAddress.Contains(k) || (u.PhoneNumber != null && u.PhoneNumber.Contains(k)));
        }
        if (!string.IsNullOrWhiteSpace(input.RoleName))
            query = query.Where(u => u.Roles.Any(r => r.Role.Name == input.RoleName));
        if (input.IsActive.HasValue)
            query = query.Where(u => u.IsActive == input.IsActive.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(u => u.CreationTime).ThenByDescending(u => u.Id)
            .PageBy(input)
            .Select(u => new UserDto
            {
                Id = u.Id,
                UserName = u.UserName,
                Name = u.Name,
                EmailAddress = u.EmailAddress,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive,
                CreationTime = u.CreationTime,
                RoleNames = u.Roles.Select(r => r.Role.Name).ToList()
            })
            .ToListAsync();
        return new PagedResultDto<UserDto>(totalCount, items);
    }

    public async Task<UserDto> GetAsync(long id) => ToDto(await LoadUserAsync(id));

    public async Task<ListResultDto<RoleDto>> GetRolesAsync()
    {
        var roles = await _roleRepository.GetAll().OrderBy(r => r.Id)
            .Select(r => new RoleDto { Id = r.Id, Name = r.Name, DisplayName = r.DisplayName })
            .ToListAsync();
        return new ListResultDto<RoleDto>(roles);
    }

    /// <summary>Quản trị viên tạo tài khoản (thường là tài khoản Nhân viên).</summary>
    public async Task<UserDto> CreateAsync(CreateUserInput input)
    {
        PasswordPolicy.Validate(input.Password);
        var email = input.EmailAddress.Trim().ToLowerInvariant();
        var userName = string.IsNullOrWhiteSpace(input.UserName) ? email : input.UserName.Trim();
        if (await _userRepository.GetAll().AnyAsync(u => u.EmailAddress == email || u.UserName == userName))
            throw new UserFriendlyException("Tài khoản này đã được sử dụng.");

        var roles = await ResolveRolesAsync(input.RoleNames);
        var user = new User
        {
            Name = input.Name.Trim(),
            EmailAddress = email,
            UserName = userName,
            PhoneNumber = input.PhoneNumber,
            IsActive = true
        };
        user.Password = _passwordHasher.HashPassword(user, input.Password);
        foreach (var role in roles)
            user.Roles.Add(new UserRole { RoleId = role.Id, Role = role });

        await _userRepository.InsertAsync(user);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(user);
    }

    public async Task<UserDto> SetRolesAsync(SetUserRolesInput input)
    {
        var user = await LoadUserAsync(input.UserId);
        var roles = await ResolveRolesAsync(input.RoleNames);

        if (user.Id == _session.GetUserId() && roles.All(r => r.Name != nameof(RoleName.Admin)))
            throw new UserFriendlyException("Bạn không thể tự gỡ quyền Quản trị viên của chính mình.");

        var removed = user.Roles.Where(ur => roles.All(r => r.Id != ur.RoleId)).ToList();
        foreach (var ur in removed)
        {
            user.Roles.Remove(ur);
            _userRoleRepository.Delete(ur);
        }
        var added = roles.Where(r => user.Roles.All(ur => ur.RoleId != r.Id)).ToList();
        foreach (var role in added)
            user.Roles.Add(new UserRole { RoleId = role.Id, Role = role });

        // Token cũ còn mang vai trò cũ: thu hồi để người dùng đăng nhập lại và nhận quyền mới.
        if (removed.Count > 0 || added.Count > 0)
            user.RotateSecurityStamp();

        await _unitOfWork.SaveChangesAsync();
        return ToDto(user);
    }

    /// <summary>Khóa / mở khóa tài khoản. Token của tài khoản bị khóa mất hiệu lực ngay ở request kế tiếp.</summary>
    public async Task<UserDto> SetActiveAsync(SetUserActiveInput input)
    {
        if (input.UserId == _session.GetUserId() && !input.IsActive)
            throw new UserFriendlyException("Bạn không thể khóa tài khoản của chính mình.");

        var user = await LoadUserAsync(input.UserId);
        user.IsActive = input.IsActive;
        await _unitOfWork.SaveChangesAsync();
        return ToDto(user);
    }

    /// <summary>Quản trị viên đặt lại mật khẩu cho tài khoản khác (VD nhân viên quên mật khẩu).</summary>
    public async Task ResetPasswordAsync(ResetUserPasswordInput input)
    {
        if (input.UserId == _session.GetUserId())
            throw new UserFriendlyException("Hãy dùng chức năng Đổi mật khẩu để đổi mật khẩu của chính bạn.");

        PasswordPolicy.Validate(input.NewPassword);
        var user = await LoadUserAsync(input.UserId);
        user.Password = _passwordHasher.HashPassword(user, input.NewPassword);
        user.RotateSecurityStamp(); // mọi phiên đăng nhập cũ của tài khoản đó mất hiệu lực
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<List<Role>> ResolveRolesAsync(IEnumerable<string> roleNames)
    {
        var names = roleNames.Select(n => n.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var roles = await _roleRepository.GetAll().Where(r => names.Contains(r.Name)).ToListAsync();
        var missing = names.FirstOrDefault(n => roles.All(r => !r.Name.Equals(n, StringComparison.OrdinalIgnoreCase)));
        if (missing != null)
            throw new UserFriendlyException($"Vai trò \"{missing}\" không tồn tại.");
        return roles;
    }

    private async Task<User> LoadUserAsync(long id) =>
        await _userRepository.GetAll().Include(u => u.Roles).ThenInclude(r => r.Role).FirstOrDefaultAsync(u => u.Id == id)
        ?? throw new EntityNotFoundException("Không tìm thấy người dùng.");

    private static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        UserName = u.UserName,
        Name = u.Name,
        EmailAddress = u.EmailAddress,
        PhoneNumber = u.PhoneNumber,
        IsActive = u.IsActive,
        CreationTime = u.CreationTime,
        RoleNames = u.Roles.Where(r => r.Role != null).Select(r => r.Role.Name).ToList()
    };
}

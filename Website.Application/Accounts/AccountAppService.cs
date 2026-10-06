using System.ComponentModel.DataAnnotations;
using Website.Domain;
using Website.Domain.Interfaces.Repositories;
using Website.Domain.SharedKernel;
using Website.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Website.Application.Accounts;

public class RegisterInput
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên."), StringLength(64)]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ."), StringLength(256)]
    public string EmailAddress { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(AppConsts.PhoneRegex, ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string PhoneNumber { get; set; } = null!;

    /// <summary>Không bắt buộc — mặc định dùng email làm tên đăng nhập.</summary>
    [StringLength(256)]
    public string? UserName { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    public string Password { get; set; } = null!;
}

public class AuthenticateModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email.")]
    public string UserNameOrEmailAddress { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    public string Password { get; set; } = null!;
}

public class RegisterOutput
{
    public long UserId { get; set; }
    public string UserName { get; set; } = null!;
}

/// <summary>Người dùng đã xác thực — Website.Api dùng để phát hành JWT.</summary>
public record AuthenticatedUser(long UserId, string UserName, string Name, IReadOnlyList<string> Roles);

public interface IAccountAppService
{
    Task<RegisterOutput> RegisterAsync(RegisterInput input);
    Task<AuthenticatedUser> ValidateCredentialsAsync(AuthenticateModel input);
}

public class AccountAppService : IAccountAppService
{
    private readonly IRepository<User, long> _userRepository;
    private readonly IRepository<Role, int> _roleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AccountAppService(IRepository<User, long> userRepository, IRepository<Role, int> roleRepository,
        IUnitOfWork unitOfWork, IPasswordHasher<User> passwordHasher)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<RegisterOutput> RegisterAsync(RegisterInput input)
    {
        PasswordPolicy.Validate(input.Password);

        var email = input.EmailAddress.Trim().ToLowerInvariant();
        var userName = string.IsNullOrWhiteSpace(input.UserName) ? email : input.UserName.Trim();

        if (await _userRepository.GetAll().AnyAsync(u => u.EmailAddress == email || u.UserName == userName))
            throw new UserFriendlyException("Tài khoản này đã được sử dụng.");

        // Tài khoản tự đăng ký luôn chỉ có vai trò Khách hàng.
        var customerRole = await _roleRepository.GetAll().SingleAsync(r => r.Name == StaticRoleNames.Customer);

        var user = new User
        {
            Name = input.Name.Trim(),
            EmailAddress = email,
            UserName = userName,
            PhoneNumber = input.PhoneNumber.Trim(),
            IsActive = true
        };
        user.Password = _passwordHasher.HashPassword(user, input.Password);
        user.Roles.Add(new UserRole { RoleId = customerRole.Id });

        await _userRepository.InsertAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return new RegisterOutput { UserId = user.Id, UserName = user.UserName };
    }

    public async Task<AuthenticatedUser> ValidateCredentialsAsync(AuthenticateModel input)
    {
        var login = input.UserNameOrEmailAddress.Trim();
        var user = await _userRepository.GetAll()
            .Include(u => u.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(u => u.UserName == login || u.EmailAddress == login.ToLower());

        // Cùng một thông báo cho sai tên đăng nhập và sai mật khẩu để không lộ tài khoản nào tồn tại.
        if (user == null ||
            _passwordHasher.VerifyHashedPassword(user, user.Password, input.Password) == PasswordVerificationResult.Failed)
            throw new UserFriendlyException("Tên đăng nhập hoặc mật khẩu không chính xác.");

        if (!user.IsActive)
            throw new UserFriendlyException("Tài khoản của bạn đã bị khóa. Vui lòng liên hệ cửa hàng.");

        return new AuthenticatedUser(user.Id, user.UserName, user.Name,
            user.Roles.Where(r => r.Role != null).Select(r => r.Role.Name).ToList());
    }
}

public static class PasswordPolicy
{
    /// <summary>Tối thiểu 6 ký tự, có cả chữ và số.</summary>
    public static void Validate(string password)
    {
        if (password.Length < 6 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new UserFriendlyException("Mật khẩu phải có ít nhất 6 ký tự, gồm cả chữ và số.");
    }
}

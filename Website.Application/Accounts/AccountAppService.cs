// Thư viện DataAnnotations: các thuộc tính kiểm tra dữ liệu đầu vào ([Required], [StringLength], [EmailAddress]...).
using System.ComponentModel.DataAnnotations;
// WebUtility.HtmlEncode: mã hóa tên người dùng và link trước khi chèn vào nội dung email HTML.
using System.Net;
// IEmailSender (gửi email) và AppUrlOptions (địa chỉ hai frontend để tạo link trong email).
using Website.Application.Common;
// AppConsts: hằng số dùng chung — PhoneRegex, thời hạn link đặt lại mật khẩu...
using Website.Domain;
// IRepository (truy vấn / thêm / xóa thực thể) và IUnitOfWork (lưu mọi thay đổi xuống CSDL).
using Website.Domain.Interfaces.Repositories;
// UserFriendlyException: lỗi nghiệp vụ, middleware chuyển thành HTTP 400 kèm thông báo tiếng Việt.
using Website.Domain.SharedKernel;
// Các thực thể User, Role, UserRole và hằng số tên vai trò StaticRoleNames.
using Website.Domain.Users.Entities;
// IPasswordHasher / PasswordHasher: băm và kiểm tra mật khẩu (lấy từ ASP.NET Core Identity).
using Microsoft.AspNetCore.Identity;
// Các hàm truy vấn bất đồng bộ của EF Core: AnyAsync, SingleAsync, FirstOrDefaultAsync, Include...
using Microsoft.EntityFrameworkCore;
// ILogger: ghi log khi gửi email thất bại; IOptions: đọc AppUrlOptions đã đăng ký từ cấu hình.
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Website.Application.Accounts;

/// <summary>
/// Input (dữ liệu frontend gửi lên) của form Đăng ký tài khoản khách hàng (UC Đăng ký – Bảng 3.3).
/// Các thuộc tính [Required], [StringLength]... được ASP.NET Core tự kiểm tra ngay khi nhận request
/// (nhờ [ApiController]); nếu sai sẽ trả HTTP 400 kèm danh sách lỗi mà không chạy vào service.
/// </summary>
public class RegisterInput
{
    /// <summary>
    /// Họ tên khách hàng. [Required]: bắt buộc nhập (báo "Vui lòng nhập họ tên.");
    /// [StringLength(64)]: tối đa 64 ký tự, khớp độ dài cột Name trong bảng AbpUsers.
    /// </summary>
    // "= null!" : giá trị khởi tạo null kèm dấu "!" (null-forgiving) để trình biên dịch không cảnh báo
    // "thuộc tính không-null chưa được gán" — giá trị thật sẽ được gán từ JSON frontend gửi lên.
    [Required(ErrorMessage = "Vui lòng nhập họ tên."), StringLength(64)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Email của khách, dùng để đăng nhập. [Required]: bắt buộc; [EmailAddress]: phải đúng định dạng email
    /// (báo "Email không hợp lệ."); [StringLength(256)]: tối đa 256 ký tự.
    /// </summary>
    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ."), StringLength(256)]
    public string EmailAddress { get; set; } = null!;

    /// <summary>
    /// Số điện thoại di động. [Required]: bắt buộc; [RegularExpression(AppConsts.PhoneRegex)]: phải là số
    /// di động Việt Nam dạng 0xxxxxxxxx hoặc +84xxxxxxxxx (đầu số 3/5/7/8/9, sau đó 8 chữ số).
    /// </summary>
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(AppConsts.PhoneRegex, ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string PhoneNumber { get; set; } = null!;

    // [StringLength(256)]: nếu có nhập thì tối đa 256 ký tự (vi phạm → HTTP 400 tự động).
    // "string?" : dấu "?" cho biết thuộc tính được phép null (frontend có thể không gửi).
    /// <summary>Không bắt buộc — mặc định dùng email làm tên đăng nhập.</summary>
    [StringLength(256)]
    public string? UserName { get; set; }

    /// <summary>
    /// Mật khẩu dạng văn bản thường (chỉ tồn tại trong request, không bao giờ lưu xuống CSDL).
    /// [Required]: bắt buộc nhập. Độ mạnh (tối thiểu 6 ký tự, có cả chữ và số) không kiểm tra bằng attribute
    /// mà bằng PasswordPolicy.Validate trong service, để dùng lại cho Đổi mật khẩu và Admin tạo tài khoản.
    /// </summary>
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    public string Password { get; set; } = null!;
}

/// <summary>
/// Input của form Đăng nhập (UC Đăng nhập – Bảng 3.1). Tên lớp và tên trường giữ theo quy ước ABP
/// để frontend gọi API /api/TokenAuth/Authenticate như hệ thống ABP chuẩn.
/// </summary>
public class AuthenticateModel
{
    /// <summary>
    /// Người dùng có thể nhập tên đăng nhập HOẶC email. [Required]: bắt buộc nhập (vi phạm → HTTP 400 tự động).
    /// </summary>
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email.")]
    public string UserNameOrEmailAddress { get; set; } = null!;

    /// <summary>Mật khẩu người dùng nhập. [Required]: bắt buộc nhập (vi phạm → HTTP 400 tự động).</summary>
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    public string Password { get; set; } = null!;
}

/// <summary>Input của form Quên mật khẩu: email đã đăng ký.</summary>
public class ForgotPasswordInput
{
    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ."), StringLength(256)]
    public string EmailAddress { get; set; } = null!;
}

/// <summary>Input của trang Đặt lại mật khẩu: mã lấy từ link trong email và mật khẩu mới.</summary>
public class ResetPasswordInput
{
    [Required(ErrorMessage = PasswordResetToken.InvalidMessage)]
    public string Token { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    public string NewPassword { get; set; } = null!;
}

/// <summary>
/// DTO (đối tượng truyền dữ liệu về frontend) trả về sau khi Đăng ký thành công (Bảng 3.3):
/// frontend dùng để báo thành công và điền sẵn tên đăng nhập vào form Đăng nhập.
/// </summary>
public class RegisterOutput
{
    /// <summary>Id của tài khoản vừa tạo (CSDL tự sinh khi INSERT).</summary>
    public long UserId { get; set; }
    /// <summary>Tên đăng nhập của tài khoản vừa tạo (bằng email nếu khách không nhập tên đăng nhập).</summary>
    public string UserName { get; set; } = null!;
}

// "record" là kiểu dữ liệu C# chuyên để chứa dữ liệu bất biến (immutable). Cú pháp "record Ten(kiểu a, kiểu b)"
// (positional record) để trình biên dịch tự sinh hàm khởi tạo, các thuộc tính chỉ đọc UserId, UserName, Name, Roles,
// và phép so sánh theo giá trị. Đối tượng này KHÔNG trả thẳng cho frontend: TokenAuthController nhận nó,
// gọi TokenService tạo JWT (claim Id, tên đăng nhập, các vai trò) rồi trả về accessToken.
/// <summary>Người dùng đã xác thực — Website.Api dùng để phát hành JWT.</summary>
public record AuthenticatedUser(long UserId, string UserName, string Name, IReadOnlyList<string> Roles, string SecurityStamp);

/// <summary>
/// Hợp đồng (interface) của Application Service quản lý tài khoản. Controller chỉ phụ thuộc vào interface này;
/// lớp cài đặt AccountAppService được gắn vào qua Dependency Injection (ApplicationModule) — dễ thay thế và kiểm thử.
/// </summary>
public interface IAccountAppService
{
    // "async Task<T>": phương thức bất đồng bộ trả về T trong tương lai. Trong lúc chờ CSDL trả kết quả,
    // luồng xử lý được giải phóng để phục vụ request khác, giúp máy chủ chịu tải tốt hơn.
    /// <summary>Đăng ký tài khoản khách hàng mới (Bảng 3.3).</summary>
    /// <param name="input">Thông tin khách nhập trên form Đăng ký.</param>
    /// <returns>Id và tên đăng nhập của tài khoản vừa tạo.</returns>
    Task<RegisterOutput> RegisterAsync(RegisterInput input);
    /// <summary>Kiểm tra tên đăng nhập/email và mật khẩu khi Đăng nhập (Bảng 3.1).</summary>
    /// <param name="input">Tên đăng nhập hoặc email và mật khẩu.</param>
    /// <returns>Thông tin người dùng hợp lệ để tầng Api phát hành JWT.</returns>
    Task<AuthenticatedUser> ValidateCredentialsAsync(AuthenticateModel input);
    /// <summary>
    /// UC Quên mật khẩu: gửi email chứa link đặt lại mật khẩu. Luôn kết thúc "thành công" dù email có tồn tại
    /// hay không, để không ai dò được email nào đã đăng ký.
    /// </summary>
    Task ForgotPasswordAsync(ForgotPasswordInput input);
    /// <summary>Đặt mật khẩu mới bằng mã trong link email; mã sai / đã dùng / hết hạn → lỗi nghiệp vụ.</summary>
    Task ResetPasswordAsync(ResetPasswordInput input);
}

public class AccountAppService : IAccountAppService
{
    private readonly IRepository<User, long> _userRepository;
    private readonly IRepository<Role, int> _roleRepository;
    private readonly IRepository<PasswordResetToken, long> _resetTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IEmailSender _emailSender;
    private readonly AppUrlOptions _urls;
    private readonly ILogger<AccountAppService> _logger;

    public AccountAppService(IRepository<User, long> userRepository, IRepository<Role, int> roleRepository,
        IRepository<PasswordResetToken, long> resetTokenRepository, IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher, IEmailSender emailSender, IOptions<AppUrlOptions> urls,
        ILogger<AccountAppService> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _resetTokenRepository = resetTokenRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _emailSender = emailSender;
        _urls = urls.Value;
        _logger = logger;
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
            user.Roles.Where(r => r.Role != null).Select(r => r.Role.Name).ToList(), user.SecurityStamp);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordInput input)
    {
        var email = input.EmailAddress.Trim().ToLowerInvariant();
        var user = await _userRepository.GetAll()
            .Include(u => u.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(u => u.EmailAddress == email);

        // Email chưa đăng ký hoặc tài khoản bị khóa: không gửi gì nhưng vẫn trả thành công như bình thường.
        if (user == null || !user.IsActive)
            return;

        var now = DateTime.Now;
        var pending = await _resetTokenRepository.GetAll()
            .Where(t => t.UserId == user.Id && t.UsedTime == null && t.ExpirationTime > now)
            .ToListAsync();

        // Chống spam: vừa gửi trong vòng PasswordResetResendSeconds giây thì không gửi thêm.
        if (pending.Any(t => t.CreationTime > now.AddSeconds(-AppConsts.PasswordResetResendSeconds)))
            return;

        // Chỉ link mới nhất còn dùng được.
        foreach (var old in pending)
            old.Revoke(now);

        var (token, rawToken) = PasswordResetToken.Issue(
            user.Id, now, TimeSpan.FromMinutes(AppConsts.PasswordResetTokenLifetimeMinutes));
        await _resetTokenRepository.InsertAsync(token);
        await _unitOfWork.SaveChangesAsync();

        try
        {
            // Nhân viên / Quản trị viên nhận link về trang quản trị, khách hàng về trang cửa hàng.
            var isStaff = user.Roles.Any(r => r.Role?.Name is StaticRoleNames.Admin or StaticRoleNames.Staff);
            var baseUrl = isStaff && !string.IsNullOrWhiteSpace(_urls.AdminRootAddress)
                ? _urls.AdminRootAddress
                : _urls.ClientRootAddress;
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("Thiếu cấu hình App:ClientRootAddress / App:AdminRootAddress.");

            var link = $"{baseUrl.TrimEnd('/')}/reset-password?token={rawToken}";
            await _emailSender.SendAsync(user.EmailAddress, "Đặt lại mật khẩu – Hồn Sừng Việt", BuildResetEmail(user.Name, link));
        }
        catch (Exception ex)
        {
            // Không báo lỗi ra ngoài (sẽ lộ email này có tồn tại); thu hồi mã để người dùng gửi lại được ngay.
            _logger.LogError(ex, "Không gửi được email đặt lại mật khẩu cho người dùng {UserId}", user.Id);
            token.Revoke(now);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordInput input)
    {
        var now = DateTime.Now;
        var hash = PasswordResetToken.Hash(input.Token);
        var token = await _resetTokenRepository.GetAll().FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (token == null || !token.IsUsable(now))
            throw new UserFriendlyException(PasswordResetToken.InvalidMessage);

        var user = await _userRepository.GetAll().FirstOrDefaultAsync(u => u.Id == token.UserId && u.IsActive)
                   ?? throw new UserFriendlyException(PasswordResetToken.InvalidMessage);

        // Kiểm tra độ mạnh trước khi dùng mã: mật khẩu yếu thì nhập lại được mà không phải xin link mới.
        PasswordPolicy.Validate(input.NewPassword);
        user.Password = _passwordHasher.HashPassword(user, input.NewPassword);
        user.RotateSecurityStamp(); // mọi phiên đăng nhập cũ mất hiệu lực
        token.MarkUsed(now);

        // Các link khác của tài khoản này cũng hết hiệu lực.
        var others = await _resetTokenRepository.GetAll()
            .Where(t => t.UserId == user.Id && t.Id != token.Id && t.UsedTime == null)
            .ToListAsync();
        foreach (var other in others)
            other.Revoke(now);

        await _unitOfWork.SaveChangesAsync();
    }

    private static string BuildResetEmail(string name, string link)
    {
        var safeName = WebUtility.HtmlEncode(name);
        var safeLink = WebUtility.HtmlEncode(link);
        return $"""
            <div style="font-family:Arial,sans-serif;font-size:15px;line-height:1.6;color:#1f2933">
              <p>Xin chào {safeName},</p>
              <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản Hồn Sừng Việt của bạn.</p>
              <p><a href="{safeLink}" style="display:inline-block;padding:10px 20px;background:#14284b;color:#ffffff;text-decoration:none;border-radius:8px">Đặt lại mật khẩu</a></p>
              <p>Hoặc mở liên kết: <br><a href="{safeLink}">{safeLink}</a></p>
              <p>Liên kết có hiệu lực trong {AppConsts.PasswordResetTokenLifetimeMinutes} phút và chỉ dùng được một lần.</p>
              <p>Nếu bạn không yêu cầu, hãy bỏ qua email này — mật khẩu của bạn vẫn giữ nguyên.</p>
            </div>
            """;
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

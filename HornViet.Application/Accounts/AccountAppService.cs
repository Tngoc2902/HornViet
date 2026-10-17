// Thư viện DataAnnotations: các thuộc tính kiểm tra dữ liệu đầu vào ([Required], [StringLength], [EmailAddress]...).
using System.ComponentModel.DataAnnotations;
// WebUtility.HtmlEncode: mã hóa tên người dùng và link trước khi chèn vào nội dung email HTML.
using System.Net;
// IEmailSender (gửi email) và AppUrlOptions (địa chỉ hai frontend để tạo link trong email).
using HornViet.Application.Common;
// AppConsts: hằng số dùng chung — PhoneRegex, thời hạn link đặt lại mật khẩu...
using HornViet.Domain;
// IRepository (truy vấn / thêm / xóa thực thể) và IUnitOfWork (lưu mọi thay đổi xuống CSDL).
using HornViet.Domain.Interfaces.Repositories;
// UserFriendlyException: lỗi nghiệp vụ, middleware chuyển thành HTTP 400 kèm thông báo tiếng Việt.
using HornViet.Domain.SharedKernel;
// Các thực thể User, Role, UserRole, PasswordResetToken.
using HornViet.Domain.Users.Entities;
// IPasswordHasher / PasswordHasher: băm và kiểm tra mật khẩu (lấy từ ASP.NET Core Identity).
using Microsoft.AspNetCore.Identity;
// Các hàm truy vấn bất đồng bộ của EF Core: AnyAsync, SingleAsync, FirstOrDefaultAsync, Include...
using Microsoft.EntityFrameworkCore;
// ILogger: ghi log khi gửi email thất bại; IOptions: đọc AppUrlOptions đã đăng ký từ cấu hình.
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HornViet.Domain.Users.Enums;

namespace HornViet.Application.Accounts;

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

/// <summary>
/// Input của nút "Đăng nhập bằng Google": access token Google cấp cho trang cửa hàng sau khi khách chọn tài khoản.
/// Backend tự hỏi Google để kiểm tra token này, không tin thông tin nào khác từ frontend.
/// </summary>
public class GoogleLoginInput
{
    [Required(ErrorMessage = "Thiếu thông tin xác thực Google.")]
    public string AccessToken { get; set; } = null!;
}

/// <summary>Input của form Quên mật khẩu: email đã đăng ký.</summary>
public class ForgotPasswordInput
{
    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ."), StringLength(256)]
    public string EmailAddress { get; set; } = null!;
}

/// <summary>Input bước nhập mã xác nhận: email và mã 6 số nhận qua email.</summary>
public class VerifyResetCodeInput
{
    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ."), StringLength(256)]
    public string EmailAddress { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập mã xác nhận."), StringLength(16)]
    public string Code { get; set; } = null!;
}

/// <summary>Kết quả nhập đúng mã: "vé" dùng một lần để đặt mật khẩu mới (hiệu lực vài phút).</summary>
public class VerifyResetCodeOutput
{
    public string ResetToken { get; set; } = null!;
}

/// <summary>Input bước đặt mật khẩu mới: vé nhận được sau khi nhập đúng mã và mật khẩu mới.</summary>
public class ResetPasswordInput
{
    [Required(ErrorMessage = PasswordResetToken.InvalidTicketMessage)]
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
/// <summary>Người dùng đã xác thực — HornViet.Api dùng để phát hành JWT.</summary>
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
    /// Đăng nhập bằng Google: kiểm tra token với Google, dùng tài khoản đã liên kết với Google hoặc cùng email (liên kết luôn);
    /// chưa có thì tạo tài khoản Khách hàng mới chưa có mật khẩu.
    /// </summary>
    Task<AuthenticatedUser> AuthenticateWithGoogleAsync(GoogleLoginInput input);
    /// <summary>
    /// UC Quên mật khẩu, bước 1: gửi email chứa mã xác nhận 6 số (hiệu lực 60 giây). Luôn kết thúc "thành công" dù
    /// email có tồn tại hay không, để không ai dò được email nào đã đăng ký.
    /// </summary>
    Task ForgotPasswordAsync(ForgotPasswordInput input);
    /// <summary>
    /// Bước 2: kiểm tra mã xác nhận; đúng thì cấp "vé" để đặt mật khẩu mới. Sai quá số lần cho phép thì thu hồi mã.
    /// </summary>
    Task<VerifyResetCodeOutput> VerifyResetCodeAsync(VerifyResetCodeInput input);
    /// <summary>Bước 3: đặt mật khẩu mới bằng vé; vé sai / đã dùng / hết hạn → lỗi nghiệp vụ.</summary>
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
    private readonly IRealtimeNotifier _notifier;
    private readonly IGoogleTokenValidator _google;

    public AccountAppService(IRepository<User, long> userRepository, IRepository<Role, int> roleRepository,
        IRepository<PasswordResetToken, long> resetTokenRepository, IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher, IEmailSender emailSender, IOptions<AppUrlOptions> urls,
        ILogger<AccountAppService> logger, IRealtimeNotifier notifier, IGoogleTokenValidator google)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _resetTokenRepository = resetTokenRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _emailSender = emailSender;
        _urls = urls.Value;
        _logger = logger;
        _notifier = notifier;
        _google = google;
    }

    public async Task<RegisterOutput> RegisterAsync(RegisterInput input)
    {
        PasswordPolicy.Validate(input.Password);

        var email = input.EmailAddress.Trim().ToLowerInvariant();
        var userName = string.IsNullOrWhiteSpace(input.UserName) ? email : input.UserName.Trim();

        if (await _userRepository.GetAll().AnyAsync(u => u.EmailAddress == email || u.UserName == userName))
            throw new UserFriendlyException("Tài khoản này đã được sử dụng.");

        // Tài khoản tự đăng ký luôn chỉ có vai trò Khách hàng.
        var customerRole = await _roleRepository.GetAll().SingleAsync(r => r.Name == nameof(RoleName.Customer));

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
        // Tài khoản tạo bằng Google chưa có mật khẩu → cũng báo sai như vậy.
        if (user == null || !user.HasPassword ||
            _passwordHasher.VerifyHashedPassword(user, user.Password, input.Password) == PasswordVerificationResult.Failed)
            throw new UserFriendlyException("Tên đăng nhập hoặc mật khẩu không chính xác.");

        if (!user.IsActive)
            throw new UserFriendlyException("Tài khoản của bạn đã bị khóa. Vui lòng liên hệ cửa hàng.");

        return ToAuthenticatedUser(user);
    }

    public async Task<AuthenticatedUser> AuthenticateWithGoogleAsync(GoogleLoginInput input)
    {
        if (!_google.IsEnabled)
            throw new UserFriendlyException("Cửa hàng chưa bật đăng nhập bằng Google.");

        var google = await _google.ValidateAsync(input.AccessToken)
                     ?? throw new UserFriendlyException("Không xác thực được tài khoản Google. Vui lòng thử lại.");
        // Chỉ tin email Google đã xác minh; nếu không, ai cũng có thể dùng email của người khác để vào tài khoản.
        if (!google.EmailVerified || string.IsNullOrWhiteSpace(google.Email))
            throw new UserFriendlyException("Email của tài khoản Google này chưa được xác minh.");

        var email = google.Email.Trim().ToLowerInvariant();
        var users = _userRepository.GetAll().Include(u => u.Roles).ThenInclude(r => r.Role);
        // Ưu tiên tài khoản đã liên kết với Google này; chưa có thì tìm tài khoản đăng ký bằng cùng email.
        var user = await users.FirstOrDefaultAsync(u => u.GoogleId == google.Subject)
                   ?? await users.FirstOrDefaultAsync(u => u.EmailAddress == email);

        if (user == null)
            return await CreateGoogleCustomerAsync(google, email);

        if (user.Roles.Any(r => r.Role?.Name is nameof(RoleName.Admin) or nameof(RoleName.Staff)))
            throw new UserFriendlyException("Tài khoản Nhân viên / Quản trị viên vui lòng đăng nhập bằng mật khẩu tại trang quản trị.");
        if (!user.IsActive)
            throw new UserFriendlyException("Tài khoản của bạn đã bị khóa. Vui lòng liên hệ cửa hàng.");

        if (user.GoogleId == null)
        {
            // Lần đầu dùng Google với tài khoản đăng ký bằng email: liên kết luôn, lần sau tìm theo mã Google.
            user.GoogleId = google.Subject;
            await _unitOfWork.SaveChangesAsync();
        }
        else if (user.GoogleId != google.Subject)
            throw new UserFriendlyException("Email này đã được liên kết với một tài khoản Google khác.");

        return ToAuthenticatedUser(user);
    }

    /// <summary>Tạo tài khoản Khách hàng từ thông tin Google: chưa có mật khẩu và số điện thoại (khách bổ sung sau).</summary>
    private async Task<AuthenticatedUser> CreateGoogleCustomerAsync(GoogleUserInfo google, string email)
    {
        var customerRole = await _roleRepository.GetAll().SingleAsync(r => r.Name == nameof(RoleName.Customer));
        var name = string.IsNullOrWhiteSpace(google.Name) ? email.Split('@')[0] : google.Name.Trim();

        var user = new User
        {
            Name = name.Length > 64 ? name[..64] : name,
            EmailAddress = email,
            // Tên đăng nhập là email như khi tự đăng ký; email đó đã là tên đăng nhập của người khác (hiếm) thì dùng mã Google.
            UserName = await _userRepository.GetAll().AnyAsync(u => u.UserName == email) ? $"google_{google.Subject}" : email,
            Password = "", // muốn đăng nhập bằng mật khẩu thì tạo qua Quên mật khẩu (link gửi về email Google)
            GoogleId = google.Subject,
            IsActive = true
        };
        user.Roles.Add(new UserRole { RoleId = customerRole.Id });

        await _userRepository.InsertAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return new AuthenticatedUser(user.Id, user.UserName, user.Name, new[] { customerRole.Name }, user.SecurityStamp);
    }

    private static AuthenticatedUser ToAuthenticatedUser(User user) =>
        new(user.Id, user.UserName, user.Name,
            user.Roles.Where(r => r.Role != null).Select(r => r.Role.Name).ToList(), user.SecurityStamp);

    public async Task ForgotPasswordAsync(ForgotPasswordInput input)
    {
        var email = input.EmailAddress.Trim().ToLowerInvariant();
        var user = await _userRepository.GetAll().FirstOrDefaultAsync(u => u.EmailAddress == email);

        // Email chưa đăng ký hoặc tài khoản bị khóa: không gửi gì nhưng vẫn trả thành công như bình thường.
        if (user == null || !user.IsActive)
            return;

        var now = DateTime.Now;
        var pending = await _resetTokenRepository.GetAll()
            .Where(t => t.UserId == user.Id && t.Purpose == PasswordResetPurpose.Code && t.UsedTime == null && t.ExpirationTime > now)
            .ToListAsync();

        // Chống spam: vừa gửi trong vòng PasswordResetResendSeconds giây thì không gửi thêm.
        if (pending.Any(t => t.CreationTime > now.AddSeconds(-AppConsts.PasswordResetResendSeconds)))
            return;

        // Chỉ mã mới nhất còn dùng được.
        foreach (var old in pending)
            old.Revoke(now);

        var (token, rawCode) = PasswordResetToken.IssueCode(
            user.Id, now, TimeSpan.FromSeconds(AppConsts.PasswordResetCodeLifetimeSeconds));
        await _resetTokenRepository.InsertAsync(token);
        await _unitOfWork.SaveChangesAsync();

        try
        {
            await _emailSender.SendAsync(user.EmailAddress, "Mã xác nhận đặt lại mật khẩu – Hồn Sừng Việt", BuildCodeEmail(user.Name, rawCode));
        }
        catch (Exception ex)
        {
            // Không báo lỗi ra ngoài (sẽ lộ email này có tồn tại); thu hồi mã để người dùng gửi lại được ngay.
            _logger.LogError(ex, "Không gửi được mã xác nhận cho người dùng {UserId}. Kiểm tra cấu hình gửi email (Smtp hoặc Email:Provider)", user.Id);
            token.Revoke(now);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task<VerifyResetCodeOutput> VerifyResetCodeAsync(VerifyResetCodeInput input)
    {
        var now = DateTime.Now;
        var email = input.EmailAddress.Trim().ToLowerInvariant();
        var user = await _userRepository.GetAll().FirstOrDefaultAsync(u => u.EmailAddress == email && u.IsActive);

        // Cùng một thông báo cho email không tồn tại, mã sai, hết hạn hay bị khóa vì nhập sai quá nhiều.
        var code = user == null ? null : await _resetTokenRepository.GetAll()
            .Where(t => t.UserId == user.Id && t.Purpose == PasswordResetPurpose.Code && t.UsedTime == null && t.ExpirationTime > now)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync();
        if (user == null || code == null)
            throw new UserFriendlyException(PasswordResetToken.InvalidMessage);

        if (code.TokenHash != PasswordResetToken.HashCode(user.Id, input.Code))
        {
            code.RegisterFailure(now, AppConsts.PasswordResetCodeMaxAttempts);
            await _unitOfWork.SaveChangesAsync();
            throw new UserFriendlyException(PasswordResetToken.InvalidMessage);
        }

        // Mã đúng: dùng một lần, các vé cũ hết hiệu lực, cấp vé mới để nhập mật khẩu mới.
        code.MarkUsed(now);
        var oldTickets = await _resetTokenRepository.GetAll()
            .Where(t => t.UserId == user.Id && t.Purpose == PasswordResetPurpose.Ticket && t.UsedTime == null)
            .ToListAsync();
        foreach (var old in oldTickets)
            old.Revoke(now);

        var (ticket, rawTicket) = PasswordResetToken.Issue(
            user.Id, now, TimeSpan.FromMinutes(AppConsts.PasswordResetTicketLifetimeMinutes));
        await _resetTokenRepository.InsertAsync(ticket);
        await _unitOfWork.SaveChangesAsync();
        return new VerifyResetCodeOutput { ResetToken = rawTicket };
    }

    public async Task ResetPasswordAsync(ResetPasswordInput input)
    {
        var now = DateTime.Now;
        var hash = PasswordResetToken.Hash(input.Token);
        // Chỉ nhận vé (Purpose = Ticket): mã 6 số không đặt được mật khẩu và không bỏ qua bước giới hạn nhập sai.
        var token = await _resetTokenRepository.GetAll()
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == PasswordResetPurpose.Ticket);
        if (token == null || !token.IsUsable(now))
            throw new UserFriendlyException(PasswordResetToken.InvalidTicketMessage);

        var user = await _userRepository.GetAll().FirstOrDefaultAsync(u => u.Id == token.UserId && u.IsActive)
                   ?? throw new UserFriendlyException(PasswordResetToken.InvalidTicketMessage);

        // Kiểm tra độ mạnh trước khi dùng vé: mật khẩu yếu thì nhập lại được mà không phải làm lại từ đầu.
        PasswordPolicy.Validate(input.NewPassword);
        user.Password = _passwordHasher.HashPassword(user, input.NewPassword);
        user.RotateSecurityStamp(); // mọi phiên đăng nhập cũ mất hiệu lực
        token.MarkUsed(now);

        // Các mã / vé khác của tài khoản này cũng hết hiệu lực.
        var others = await _resetTokenRepository.GetAll()
            .Where(t => t.UserId == user.Id && t.Id != token.Id && t.UsedTime == null)
            .ToListAsync();
        foreach (var other in others)
            other.Revoke(now);

        await _unitOfWork.SaveChangesAsync();
        await _notifier.SessionRevokedAsync(user.Id, SessionRevokedReason.PasswordReset); // các tab / thiết bị đang mở đăng xuất ngay
    }

    private static string BuildCodeEmail(string name, string code)
    {
        var safeName = WebUtility.HtmlEncode(name);
        return $"""
            <div style="font-family:Arial,sans-serif;font-size:15px;line-height:1.6;color:#1f2933">
              <p>Xin chào {safeName},</p>
              <p>Mã xác nhận đặt lại mật khẩu tài khoản Hồn Sừng Việt của bạn là:</p>
              <p style="font-size:32px;font-weight:bold;letter-spacing:8px;color:#14284b;margin:12px 0">{code}</p>
              <p>Mã có hiệu lực trong <b>{AppConsts.PasswordResetCodeLifetimeSeconds} giây</b> và chỉ dùng được một lần.</p>
              <p>Nếu bạn không yêu cầu, hãy bỏ qua email này — mật khẩu của bạn vẫn giữ nguyên. Không chia sẻ mã này với bất kỳ ai.</p>
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

using System.Security.Cryptography;
using System.Text;
using HornViet.Domain.SharedKernel;

namespace HornViet.Domain.Users.Entities;

/// <summary>Loại bản ghi trong bảng PasswordResetTokens.</summary>
public enum PasswordResetPurpose
{
    /// <summary>Mã xác nhận 6 số gửi qua email, hiệu lực ngắn (60 giây), giới hạn số lần nhập sai.</summary>
    Code = 0,

    /// <summary>"Vé" ngẫu nhiên cấp sau khi nhập đúng mã, dùng để đặt mật khẩu mới.</summary>
    Ticket = 1
}

/// <summary>
/// Mã / vé đặt lại mật khẩu (UC Quên mật khẩu, bảng PasswordResetTokens). Luồng: nhập email → hệ thống gửi mã 6 số
/// (hiệu lực 60 giây) → nhập đúng mã thì nhận một vé → đặt mật khẩu mới bằng vé đó. CSDL chỉ lưu bản băm SHA-256 nên
/// lộ CSDL cũng không dùng được. Mỗi mã / vé dùng một lần và có hạn dùng.
/// </summary>
public class PasswordResetToken : Entity<long>, IHasCreationTime
{
    /// <summary>Thông báo chung cho mã sai, đã dùng hoặc hết hạn — không cho biết cụ thể lý do.</summary>
    public const string InvalidMessage = "Mã xác nhận không đúng hoặc đã hết hạn. Vui lòng yêu cầu mã mới.";

    /// <summary>Thông báo khi vé đặt lại mật khẩu không còn dùng được (hết hạn hoặc đã dùng).</summary>
    public const string InvalidTicketMessage = "Phiên đặt lại mật khẩu đã hết hạn. Vui lòng thực hiện lại từ đầu.";

    /// <summary>Tài khoản được đặt lại mật khẩu.</summary>
    public long UserId { get; private set; }

    /// <summary>Mã xác nhận hay vé đặt lại mật khẩu.</summary>
    public PasswordResetPurpose Purpose { get; private set; }

    /// <summary>SHA-256 của mã gốc (kèm Id người dùng với mã 6 số), dạng hex 64 ký tự.</summary>
    public string TokenHash { get; private set; } = null!;

    /// <summary>Sau thời điểm này mã không còn dùng được.</summary>
    public DateTime ExpirationTime { get; private set; }

    /// <summary>Thời điểm mã đã được dùng hoặc bị thu hồi; null = còn dùng được (nếu chưa hết hạn).</summary>
    public DateTime? UsedTime { get; private set; }

    /// <summary>Số lần nhập sai mã này — chống dò mã 6 số.</summary>
    public int FailedAttempts { get; private set; }

    /// <summary>Thời điểm tạo mã — dùng để chặn gửi email lặp lại quá nhanh.</summary>
    public DateTime CreationTime { get; set; }

    private PasswordResetToken() { }

    /// <summary>
    /// Phát hành mã xác nhận 6 số mới: trả về thực thể để lưu và mã gốc để đặt vào email. Mã 6 số chỉ có 1 triệu khả năng
    /// nên an toàn nhờ hiệu lực ngắn và giới hạn số lần nhập sai (<see cref="RegisterFailure"/>).
    /// </summary>
    public static (PasswordResetToken Token, string RawCode) IssueCode(long userId, DateTime now, TimeSpan lifetime)
    {
        var rawCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var token = new PasswordResetToken
        {
            UserId = userId,
            Purpose = PasswordResetPurpose.Code,
            TokenHash = HashCode(userId, rawCode),
            ExpirationTime = now.Add(lifetime),
            CreationTime = now
        };
        return (token, rawCode);
    }

    /// <summary>
    /// Phát hành vé đặt lại mật khẩu: trả về thực thể để lưu và vé gốc (32 byte ngẫu nhiên, hex) để trả cho trình duyệt.
    /// </summary>
    public static (PasswordResetToken Token, string RawToken) Issue(long userId, DateTime now, TimeSpan lifetime)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var token = new PasswordResetToken
        {
            UserId = userId,
            Purpose = PasswordResetPurpose.Ticket,
            TokenHash = Hash(rawToken),
            ExpirationTime = now.Add(lifetime),
            CreationTime = now
        };
        return (token, rawToken);
    }

    /// <summary>Băm mã gốc. Vé đủ ngẫu nhiên (256 bit) nên dùng SHA-256 là đủ, không cần hàm băm chậm như mật khẩu.</summary>
    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken.Trim().ToUpperInvariant())));

    /// <summary>Băm mã 6 số kèm Id người dùng: hai người cùng nhận một mã vẫn ra hai bản băm khác nhau (cột TokenHash là duy nhất).</summary>
    public static string HashCode(long userId, string rawCode) => Hash($"{userId}:{rawCode.Trim()}");

    /// <summary>Còn dùng được: chưa dùng / chưa thu hồi và chưa hết hạn.</summary>
    public bool IsUsable(DateTime now) => UsedTime == null && now < ExpirationTime;

    /// <summary>Dùng mã để đặt mật khẩu mới; mã đã dùng, bị thu hồi hoặc hết hạn → lỗi nghiệp vụ.</summary>
    public void MarkUsed(DateTime now)
    {
        if (!IsUsable(now))
            throw new UserFriendlyException(Purpose == PasswordResetPurpose.Ticket ? InvalidTicketMessage : InvalidMessage);
        UsedTime = now;
    }

    /// <summary>Ghi nhận một lần nhập sai; đủ <paramref name="maxAttempts"/> lần thì thu hồi mã.</summary>
    public void RegisterFailure(DateTime now, int maxAttempts)
    {
        FailedAttempts++;
        if (FailedAttempts >= maxAttempts)
            Revoke(now);
    }

    /// <summary>Thu hồi mã chưa dùng (khi phát hành mã mới hoặc mật khẩu vừa được đặt lại).</summary>
    public void Revoke(DateTime now) => UsedTime ??= now;
}

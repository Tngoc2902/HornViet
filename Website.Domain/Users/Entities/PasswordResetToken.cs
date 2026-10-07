using System.Security.Cryptography;
using System.Text;
using Website.Domain.SharedKernel;

namespace Website.Domain.Users.Entities;

/// <summary>
/// Mã đặt lại mật khẩu (UC Quên mật khẩu, bảng PasswordResetTokens). Mã gốc chỉ nằm trong link gửi qua email;
/// CSDL chỉ lưu bản băm SHA-256 nên lộ CSDL cũng không dùng được link. Mỗi mã dùng một lần và có hạn dùng.
/// </summary>
public class PasswordResetToken : Entity<long>, IHasCreationTime
{
    /// <summary>Thông báo chung cho mã sai, đã dùng hoặc hết hạn — không cho biết cụ thể lý do.</summary>
    public const string InvalidMessage =
        "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới.";

    /// <summary>Tài khoản được đặt lại mật khẩu.</summary>
    public long UserId { get; private set; }

    /// <summary>SHA-256 của mã gốc, dạng hex 64 ký tự.</summary>
    public string TokenHash { get; private set; } = null!;

    /// <summary>Sau thời điểm này mã không còn dùng được.</summary>
    public DateTime ExpirationTime { get; private set; }

    /// <summary>Thời điểm mã đã được dùng hoặc bị thu hồi; null = còn dùng được (nếu chưa hết hạn).</summary>
    public DateTime? UsedTime { get; private set; }

    /// <summary>Thời điểm tạo mã — dùng để chặn gửi email lặp lại quá nhanh.</summary>
    public DateTime CreationTime { get; set; }

    private PasswordResetToken() { }

    /// <summary>
    /// Phát hành mã mới: trả về thực thể để lưu và mã gốc (32 byte ngẫu nhiên, hex) để đặt vào link email.
    /// </summary>
    public static (PasswordResetToken Token, string RawToken) Issue(long userId, DateTime now, TimeSpan lifetime)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var token = new PasswordResetToken
        {
            UserId = userId,
            TokenHash = Hash(rawToken),
            ExpirationTime = now.Add(lifetime),
            CreationTime = now
        };
        return (token, rawToken);
    }

    /// <summary>Băm mã gốc. Mã đủ ngẫu nhiên (256 bit) nên dùng SHA-256 là đủ, không cần hàm băm chậm như mật khẩu.</summary>
    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken.Trim().ToUpperInvariant())));

    /// <summary>Còn dùng được: chưa dùng / chưa thu hồi và chưa hết hạn.</summary>
    public bool IsUsable(DateTime now) => UsedTime == null && now < ExpirationTime;

    /// <summary>Dùng mã để đặt mật khẩu mới; mã đã dùng, bị thu hồi hoặc hết hạn → lỗi nghiệp vụ.</summary>
    public void MarkUsed(DateTime now)
    {
        if (!IsUsable(now))
            throw new UserFriendlyException(InvalidMessage);
        UsedTime = now;
    }

    /// <summary>Thu hồi mã chưa dùng (khi phát hành mã mới hoặc mật khẩu vừa được đặt lại).</summary>
    public void Revoke(DateTime now) => UsedTime ??= now;
}

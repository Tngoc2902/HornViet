namespace Website.Application.Common;

/// <summary>
/// Gửi email (VD link Quên mật khẩu). Interface ở tầng Application, cài đặt SMTP ở tầng Infrastructure
/// (Services/SmtpEmailSender) — Application không phụ thuộc thư viện gửi mail cụ thể.
/// </summary>
public interface IEmailSender
{
    /// <summary>Gửi một email dạng HTML tới <paramref name="toAddress"/>.</summary>
    Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default);
}

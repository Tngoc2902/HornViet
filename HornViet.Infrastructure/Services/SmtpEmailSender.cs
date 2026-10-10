using System.Net;
using System.Net.Mail;
using System.Text;
using HornViet.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HornViet.Infrastructure.Services;

/// <summary>Cấu hình gửi email (mục "Smtp" trong appsettings.json, hoặc biến môi trường Smtp__Host...).</summary>
public class SmtpOptions
{
    /// <summary>Máy chủ SMTP, VD smtp.gmail.com. Để trống = không gửi thật, lưu email thành file .eml.</summary>
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }
    /// <summary>Mật khẩu SMTP (với Gmail là App Password) — đặt qua biến môi trường, không ghi vào appsettings.json.</summary>
    public string? Password { get; set; }
    /// <summary>
    /// Địa chỉ người gửi. Để trống = dùng Smtp:UserName (Gmail, Outlook bắt buộc gửi bằng chính tài khoản đăng nhập).
    /// </summary>
    public string? FromAddress { get; set; }
    public string FromName { get; set; } = "Hồn Sừng Việt";
    /// <summary>Thư mục lưu file .eml khi chưa cấu hình Host (đường dẫn tương đối tính từ thư mục chạy ứng dụng).</summary>
    public string PickupDirectory { get; set; } = "App_Data/mails";

    /// <summary>true = gửi thật qua SMTP; false = chỉ lưu file .eml (môi trường dev chưa có tài khoản email).</summary>
    public bool SendsRealEmail => !string.IsNullOrWhiteSpace(Host);

    /// <summary>Địa chỉ người gửi thực tế: FromAddress, nếu trống thì UserName, cuối cùng là địa chỉ mặc định.</summary>
    public string EffectiveFromAddress =>
        !string.IsNullOrWhiteSpace(FromAddress) ? FromAddress
        : !string.IsNullOrWhiteSpace(UserName) && UserName.Contains('@') ? UserName
        : "no-reply@honsungviet.local";

    /// <summary>Các lỗi cấu hình khiến email không gửi được — Program.cs ghi cảnh báo lúc khởi động.</summary>
    public IEnumerable<string> GetConfigurationProblems()
    {
        if (!SendsRealEmail)
            yield break;
        if (string.IsNullOrWhiteSpace(UserName))
            yield return "Đã đặt Smtp:Host nhưng thiếu Smtp:UserName (địa chỉ email đăng nhập SMTP).";
        if (string.IsNullOrWhiteSpace(Password))
            yield return "Đã đặt Smtp:Host nhưng thiếu Smtp:Password (với Gmail là App Password 16 ký tự).";
        if (EffectiveFromAddress.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            yield return "Địa chỉ người gửi là " + EffectiveFromAddress + " — máy chủ mail thật sẽ từ chối; đặt Smtp:UserName là email thật.";
    }
}

/// <summary>
/// Gửi email qua SMTP. Khi Smtp:Host trống (môi trường dev chưa có tài khoản email), email được ghi thành file .eml
/// trong PickupDirectory — mở bằng Outlook / Thunderbird hoặc trình soạn thảo để lấy link.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.EffectiveFromAddress, _options.FromName, Encoding.UTF8),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = htmlBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };
        message.To.Add(toAddress);

        using var client = new SmtpClient();

        var host = _options.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            var directory = Path.GetFullPath(_options.PickupDirectory);
            Directory.CreateDirectory(directory);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = directory;
            await client.SendMailAsync(message, ct);
            _logger.LogWarning("Smtp:Host chưa cấu hình — email \"{Subject}\" được lưu thành file .eml trong {Directory}",
                subject, directory);
            return;
        }

        client.Host = host;
        client.Port = _options.Port;
        client.EnableSsl = _options.EnableSsl;
        client.DeliveryMethod = SmtpDeliveryMethod.Network;
        if (!string.IsNullOrEmpty(_options.UserName))
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);

        await client.SendMailAsync(message, ct);
        _logger.LogInformation("Đã gửi email \"{Subject}\" qua {Host}:{Port}", subject, _options.Host, _options.Port);
    }
}

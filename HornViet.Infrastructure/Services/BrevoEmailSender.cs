using System.Net.Http.Json;
using HornViet.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HornViet.Infrastructure.Services;

/// <summary>
/// Cấu hình gửi email qua Brevo (mục "Email" trong appsettings.json; trên server đặt bằng biến môi trường
/// Email__Provider, Email__BrevoApiKey, Email__FromAddress, Email__FromName).
/// </summary>
public class EmailOptions
{
    /// <summary>"Smtp" (mặc định) hoặc "Brevo". Brevo gửi qua HTTPS nên chạy được trên Render gói miễn phí (chặn cổng SMTP).</summary>
    public string Provider { get; set; } = "Smtp";

    /// <summary>API key của Brevo (SMTP &amp; API → API keys) — bí mật, chỉ đặt bằng biến môi trường.</summary>
    public string? BrevoApiKey { get; set; }

    /// <summary>Email người gửi — phải là địa chỉ đã xác minh trong Brevo (Senders &amp; IP → Senders).</summary>
    public string? FromAddress { get; set; }

    public string FromName { get; set; } = "Hồn Sừng Việt";

    /// <summary>Địa chỉ API của Brevo; chỉ đổi khi kiểm thử với máy chủ giả.</summary>
    public string BrevoApiUrl { get; set; } = "https://api.brevo.com/v3/smtp/email";

    public bool UsesBrevo => string.Equals(Provider, "Brevo", StringComparison.OrdinalIgnoreCase);

    /// <summary>Các lỗi cấu hình khiến Brevo không gửi được — Program.cs ghi cảnh báo lúc khởi động.</summary>
    public IEnumerable<string> GetConfigurationProblems()
    {
        if (!UsesBrevo)
            yield break;
        if (string.IsNullOrWhiteSpace(BrevoApiKey))
            yield return "Email:Provider = Brevo nhưng thiếu Email:BrevoApiKey.";
        if (string.IsNullOrWhiteSpace(FromAddress))
            yield return "Email:Provider = Brevo nhưng thiếu Email:FromAddress (email người gửi đã xác minh trong Brevo).";
    }
}

/// <summary>Gửi email qua API HTTPS của Brevo (https://developers.brevo.com/reference/sendtransacemail).</summary>
public class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly EmailOptions _options;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(HttpClient http, IOptions<EmailOptions> options, ILogger<BrevoEmailSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BrevoApiKey) || string.IsNullOrWhiteSpace(_options.FromAddress))
            throw new InvalidOperationException("Thiếu Email:BrevoApiKey hoặc Email:FromAddress.");

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.BrevoApiUrl)
        {
            Content = JsonContent.Create(new
            {
                sender = new { name = _options.FromName, email = _options.FromAddress },
                to = new[] { new { email = toAddress } },
                subject,
                htmlContent = htmlBody
            })
        };
        request.Headers.Add("api-key", _options.BrevoApiKey);
        request.Headers.Add("accept", "application/json");

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Nội dung lỗi của Brevo (VD "sender not valid", "key not found") giúp biết cấu hình sai chỗ nào; không chứa khóa.
            var detail = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Brevo trả {(int)response.StatusCode}: {detail}");
        }

        _logger.LogInformation("Đã gửi email \"{Subject}\" qua Brevo", subject);
    }
}

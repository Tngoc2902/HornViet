using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HornViet.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HornViet.Infrastructure.Services;

/// <summary>Cấu hình đăng nhập bằng Google (mục "Google" trong appsettings.json, hoặc biến môi trường Google__ClientId).</summary>
public class GoogleAuthOptions
{
    /// <summary>
    /// OAuth Client ID (loại Web application) tạo ở Google Cloud Console — trùng NEXT_PUBLIC_GOOGLE_CLIENT_ID của
    /// trang cửa hàng. Để trống = tắt đăng nhập bằng Google.
    /// </summary>
    public string? ClientId { get; set; }
}

/// <summary>
/// Kiểm tra access token Google bằng cách hỏi thẳng máy chủ Google (tokeninfo, userinfo) — không cần Client secret.
/// </summary>
public class GoogleTokenValidator : IGoogleTokenValidator
{
    private const string TokenInfoUrl = "https://oauth2.googleapis.com/tokeninfo";
    private const string UserInfoUrl = "https://openidconnect.googleapis.com/v1/userinfo";

    private readonly HttpClient _http;
    private readonly GoogleAuthOptions _options;
    private readonly ILogger<GoogleTokenValidator> _logger;

    public GoogleTokenValidator(HttpClient http, IOptions<GoogleAuthOptions> options, ILogger<GoogleTokenValidator> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_options.ClientId);

    public async Task<GoogleUserInfo?> ValidateAsync(string accessToken, CancellationToken ct = default)
    {
        if (!IsEnabled)
            return null;

        try
        {
            // 1. Token còn hạn không, cấp cho ứng dụng nào. Bắt buộc aud == ClientId: token Google cấp cho ứng dụng khác
            //    (VD lấy được từ một trang web khác) không dùng để đăng nhập vào đây được. Gửi bằng POST để token không nằm trên URL.
            using var tokenInfoResponse = await _http.PostAsync(TokenInfoUrl,
                new FormUrlEncodedContent(new Dictionary<string, string> { ["access_token"] = accessToken }), ct);
            if (!tokenInfoResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google từ chối access token (tokeninfo trả {Status})", (int)tokenInfoResponse.StatusCode);
                return null;
            }

            var info = await tokenInfoResponse.Content.ReadFromJsonAsync<TokenInfo>(ct);
            if (info == null || string.IsNullOrEmpty(info.Sub) || info.Aud != _options.ClientId)
            {
                _logger.LogWarning("Access token Google không cấp cho ứng dụng này");
                return null;
            }

            // 2. Họ tên hiển thị (tokeninfo không có). Lỗi ở bước này thì vẫn đăng nhập được, tên lấy theo email.
            using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var userInfoResponse = await _http.SendAsync(request, ct);
            var profile = userInfoResponse.IsSuccessStatusCode
                ? await userInfoResponse.Content.ReadFromJsonAsync<UserInfo>(ct)
                : null;

            return new GoogleUserInfo(info.Sub, info.Email ?? "", IsTrue(info.EmailVerified),
                profile?.Sub == info.Sub ? profile.Name : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Không kiểm tra được access token với máy chủ Google");
            return null;
        }
    }

    // tokeninfo trả email_verified dạng chuỗi "true", userinfo dạng boolean — chấp nhận cả hai.
    private static bool IsTrue(JsonElement? value) =>
        value?.ValueKind == JsonValueKind.True ||
        (value?.ValueKind == JsonValueKind.String && value.Value.GetString() == "true");

    private sealed record TokenInfo(
        [property: JsonPropertyName("aud")] string? Aud,
        [property: JsonPropertyName("sub")] string? Sub,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("email_verified")] JsonElement? EmailVerified);

    private sealed record UserInfo(
        [property: JsonPropertyName("sub")] string? Sub,
        [property: JsonPropertyName("name")] string? Name);
}

using System.Net.Http.Json;
using System.Text.Json;
using HornViet.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HornViet.Infrastructure.Services;

/// <summary>Cấu hình mục "Gemini" (appsettings / biến môi trường Gemini__ApiKey, Gemini__Model).</summary>
public class GeminiOptions
{
    /// <summary>Khóa API tạo ở Google AI Studio. Để trống = tắt, bot chỉ dùng model tự huấn luyện.</summary>
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gemini-3.5-flash-lite";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
}

/// <summary>
/// Gọi Gemini API (models.generateContent) để trả lời câu bot tự huấn luyện chưa hiểu. Yêu cầu Gemini trả JSON theo
/// lược đồ cố định { answer, productIds, suggestStaff } để hệ thống tự gắn thẻ sản phẩm và nút gặp nhân viên.
/// </summary>
public class GeminiChatAssistant : IChatAssistant
{
    private static readonly object ResponseSchema = new
    {
        type = "OBJECT",
        properties = new
        {
            answer = new { type = "STRING" },
            productIds = new { type = "ARRAY", items = new { type = "INTEGER" } },
            suggestStaff = new { type = "BOOLEAN" }
        },
        required = new[] { "answer", "productIds", "suggestStaff" }
    };

    private readonly HttpClient _http;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiChatAssistant> _logger;

    public GeminiChatAssistant(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiChatAssistant> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<AssistantReply?> AskAsync(AssistantRequest request, CancellationToken ct = default)
    {
        if (!IsEnabled)
            return null;

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = request.Instructions } } },
            contents = Contents(request),
            generationConfig = new
            {
                temperature = 0.3,
                maxOutputTokens = 2048,
                responseMimeType = "application/json",
                responseSchema = ResponseSchema
            }
        };
        var url = $"{_options.BaseUrl.TrimEnd('/')}/v1beta/models/{Uri.EscapeDataString(_options.Model)}:generateContent";
        using var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        message.Headers.Add("x-goog-api-key", _options.ApiKey);

        try
        {
            using var response = await _http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
            {
                // 429 = hết lượt gọi của gói miễn phí; không ghi nội dung trả về vì có thể lặp lại câu của khách.
                _logger.LogWarning("Gemini trả HTTP {Status} (model {Model}).", (int)response.StatusCode, _options.Model);
                return null;
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("Gemini không trả câu trả lời (có thể bị chặn bởi bộ lọc an toàn).");
                return null;
            }
            var text = string.Concat(candidates[0].GetProperty("content").GetProperty("parts").EnumerateArray()
                .Where(p => !(p.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True))
                .Select(p => p.TryGetProperty("text", out var t) ? t.GetString() : null));

            using var json = JsonDocument.Parse(text);
            var root = json.RootElement;
            var answer = root.GetProperty("answer").GetString() ?? "";
            var ids = root.TryGetProperty("productIds", out var arr) && arr.ValueKind == JsonValueKind.Array
                ? arr.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out _)).Select(e => e.GetInt32()).ToList()
                : [];
            var suggestStaff = root.TryGetProperty("suggestStaff", out var s) && s.ValueKind == JsonValueKind.True;
            return answer.Trim().Length == 0 ? null : new AssistantReply(answer.Trim(), ids, suggestStaff);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException
                                       or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Không gọi được Gemini.");
            return null;
        }
    }

    /// <summary>
    /// Lịch sử hội thoại theo định dạng Gemini: vai "user" (khách) / "model" (bot), bắt đầu bằng khách, các tin liền nhau
    /// cùng vai được gộp lại.
    /// </summary>
    private static List<object> Contents(AssistantRequest request)
    {
        var turns = request.History.SkipWhile(t => !t.FromCustomer).Append(new AssistantTurn(true, request.Question));
        var merged = new List<(string Role, List<string> Texts)>();
        foreach (var t in turns)
        {
            var role = t.FromCustomer ? "user" : "model";
            if (merged.Count > 0 && merged[^1].Role == role)
                merged[^1].Texts.Add(t.Text);
            else
                merged.Add((role, [t.Text]));
        }
        return merged.Select(m => (object)new { role = m.Role, parts = new[] { new { text = string.Join("\n", m.Texts) } } }).ToList();
    }
}

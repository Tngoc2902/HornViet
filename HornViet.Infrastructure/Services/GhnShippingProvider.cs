using System.Net.Http.Json;
using System.Text.Json;
using HornViet.Application.Common;
using HornViet.Domain.SharedKernel;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HornViet.Infrastructure.Services;

/// <summary>
/// Cấu hình Giao Hàng Nhanh (mục "Ghn"; trên server đặt bằng biến môi trường Ghn__Token, Ghn__ShopId, Ghn__BaseUrl).
/// Token và ShopId lấy ở cổng khách hàng GHN: môi trường thử nghiệm 5sao.ghn.dev, môi trường thật khachhang.ghn.vn
/// (Bảo mật → Token API &amp; IP tin cậy).
/// </summary>
public class GhnOptions
{
    /// <summary>Token API — bí mật, chỉ đặt bằng biến môi trường. Để trống = tắt tích hợp vận chuyển.</summary>
    public string? Token { get; set; }

    /// <summary>Mã cửa hàng (ShopId) đã tạo trên cổng GHN; phí ship tính từ địa chỉ lấy hàng của shop này.</summary>
    public int ShopId { get; set; }

    /// <summary>Môi trường thử nghiệm: https://dev-online-gateway.ghn.vn — môi trường thật: https://online-gateway.ghn.vn.</summary>
    public string BaseUrl { get; set; } = "https://dev-online-gateway.ghn.vn";

    /// <summary>Cân nặng mặc định của một sản phẩm (gam) khi chưa khai báo cân nặng riêng.</summary>
    public int DefaultItemWeightGrams { get; set; } = 200;

    /// <summary>Kích thước mặc định của kiện hàng (cm).</summary>
    public int DefaultLengthCm { get; set; } = 20;
    public int DefaultWidthCm { get; set; } = 15;
    public int DefaultHeightCm { get; set; } = 10;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Token) && ShopId > 0;
}

/// <summary>Gọi API Giao Hàng Nhanh (https://developer.ghn.vn): danh mục địa chỉ và tính phí vận chuyển.</summary>
public class GhnShippingProvider : IShippingProvider
{
    private static readonly TimeSpan MasterDataCache = TimeSpan.FromHours(24);

    private readonly HttpClient _http;
    private readonly GhnOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GhnShippingProvider> _logger;

    public GhnShippingProvider(HttpClient http, IOptions<GhnOptions> options, IMemoryCache cache, ILogger<GhnShippingProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    public bool IsEnabled => _options.IsConfigured;

    public int DefaultItemWeightGrams => _options.DefaultItemWeightGrams;

    public Task<IReadOnlyList<ShippingProvince>> GetProvincesAsync(CancellationToken ct = default) =>
        Cached("provinces", async () =>
        {
            var data = await SendAsync(HttpMethod.Get, "/shiip/public-api/master-data/province", null, ct);
            return (IReadOnlyList<ShippingProvince>)data.EnumerateArray()
                .Where(IsUsable)
                .Where(p => !p.GetProperty("ProvinceName").GetString()!.StartsWith("Test", StringComparison.OrdinalIgnoreCase))
                .Select(p => new ShippingProvince(p.GetProperty("ProvinceID").GetInt32(), p.GetProperty("ProvinceName").GetString()!))
                .ToList();
        });

    public Task<IReadOnlyList<ShippingDistrict>> GetDistrictsAsync(int provinceId, CancellationToken ct = default) =>
        Cached($"districts:{provinceId}", async () =>
        {
            var data = await SendAsync(HttpMethod.Post, "/shiip/public-api/master-data/district", new { province_id = provinceId }, ct);
            return (IReadOnlyList<ShippingDistrict>)data.EnumerateArray()
                .Where(IsUsable)
                .Select(d => new ShippingDistrict(d.GetProperty("DistrictID").GetInt32(), provinceId, d.GetProperty("DistrictName").GetString()!))
                .ToList();
        });

    public Task<IReadOnlyList<ShippingWard>> GetWardsAsync(int districtId, CancellationToken ct = default) =>
        Cached($"wards:{districtId}", async () =>
        {
            var data = await SendAsync(HttpMethod.Post, "/shiip/public-api/master-data/ward", new { district_id = districtId }, ct);
            // WardCode do GHN trả về dạng chuỗi (hoặc số ở một số bản) — luôn đọc về chuỗi.
            return (IReadOnlyList<ShippingWard>)data.EnumerateArray()
                .Where(IsUsable)
                .Select(w => new ShippingWard(
                    w.GetProperty("WardCode").ValueKind == JsonValueKind.String ? w.GetProperty("WardCode").GetString()! : w.GetProperty("WardCode").GetRawText(),
                    districtId,
                    w.GetProperty("WardName").GetString()!))
                .ToList();
        });

    public async Task<ShippingFee> CalculateFeeAsync(ShippingFeeRequest request, CancellationToken ct = default)
    {
        var data = await SendAsync(HttpMethod.Post, "/shiip/public-api/v2/shipping-order/fee", new
        {
            service_type_id = 2, // giao hàng nhẹ, dưới 20 kg
            to_district_id = request.ToDistrictId,
            to_ward_code = request.ToWardCode,
            weight = request.WeightGrams,
            length = _options.DefaultLengthCm,
            width = _options.DefaultWidthCm,
            height = _options.DefaultHeightCm,
            insurance_value = (int)Math.Min(request.InsuranceValue, 5_000_000m) // GHN chỉ bảo hiểm tối đa 5 triệu
        }, ct);
        return new ShippingFee(data.GetProperty("total").GetDecimal());
    }

    // Mục master-data có thể bị GHN tắt (IsEnable / Status = 0): không đưa cho khách chọn.
    private static bool IsUsable(JsonElement e) =>
        !(e.TryGetProperty("IsEnable", out var enabled) && enabled.ValueKind == JsonValueKind.Number && enabled.GetInt32() == 0) &&
        !(e.TryGetProperty("Status", out var status) && status.ValueKind == JsonValueKind.Number && status.GetInt32() == 0);

    private async Task<T> Cached<T>(string key, Func<Task<T>> load)
    {
        if (_cache.TryGetValue($"ghn:{key}", out T? hit) && hit is not null)
            return hit;
        var value = await load();
        _cache.Set($"ghn:{key}", value, MasterDataCache);
        return value;
    }

    /// <summary>Gọi GHN, kiểm tra mã trả về và trả phần "data". Lỗi GHN ghi vào log, người dùng chỉ thấy thông báo chung.</summary>
    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        if (!_options.IsConfigured)
            throw new UserFriendlyException("Cửa hàng chưa bật dịch vụ giao hàng.");

        using var request = new HttpRequestMessage(method, _options.BaseUrl.TrimEnd('/') + path);
        request.Headers.Add("Token", _options.Token);
        request.Headers.Add("ShopId", _options.ShopId.ToString());
        if (body != null)
            request.Content = JsonContent.Create(body);

        try
        {
            using var response = await _http.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GHN {Method} {Path} trả {Status}: {Body}", method, path, (int)response.StatusCode, Truncate(text));
                throw new UserFriendlyException(FriendlyMessage(text));
            }

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var code) && code.GetInt32() != 200)
            {
                _logger.LogWarning("GHN {Method} {Path} code {Code}: {Body}", method, path, code.GetInt32(), Truncate(text));
                throw new UserFriendlyException(FriendlyMessage(text));
            }

            return root.GetProperty("data").Clone();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            _logger.LogError(ex, "Không gọi được GHN {Path}", path);
            throw new UserFriendlyException("Không kết nối được dịch vụ giao hàng. Vui lòng thử lại sau.");
        }
    }

    // Lỗi GHN: chỉ cho khách thấy câu dễ hiểu. "message" của GHN thường là chuỗi kỹ thuật (VD "Lỗi gọi API: corev2_... - ..."),
    // nên chỉ dùng code_message_value (tiếng Việt, dành cho người dùng) hoặc câu ánh xạ sẵn theo mã lỗi; chi tiết đầy đủ nằm trong log.
    private static string FriendlyMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("code_message", out var cm) && cm.ValueKind == JsonValueKind.String)
                switch (cm.GetString())
                {
                    case "SHOP_INFO_ERROR":
                        return "Cửa hàng chưa cập nhật địa chỉ lấy hàng trên hệ thống giao hàng. Vui lòng liên hệ cửa hàng.";
                    case "CLIENT_NOT_OWNER_OF_SHOP":
                        return "Cấu hình giao hàng của cửa hàng chưa đúng. Vui lòng liên hệ cửa hàng.";
                    case "ROUTE_NOT_FOUND_SERVICE":
                        return "Không hỗ trợ giao hàng tới khu vực này.";
                }
            if (root.TryGetProperty("code_message_value", out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()))
                return v.GetString()!;
        }
        catch (JsonException) { }
        return "Dịch vụ giao hàng không tính được phí cho địa chỉ này.";
    }

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500];
}

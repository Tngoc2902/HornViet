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

    /// <summary>
    /// Khóa bí mật xác thực webhook: GHN gửi kèm header (tên ở WebhookHeader) có giá trị này (cấu hình "Custom headers" ở cổng
    /// GHN → Webhook configuration). Để trống = từ chối mọi webhook.
    /// </summary>
    public string? WebhookToken { get; set; }

    public string WebhookHeader { get; set; } = "X-Webhook-Token";

    /// <summary>Ghi chú cho shipper: CHOXEMHANGKHONGTHU (cho xem, không cho thử) | KHONGCHOXEMHANG | CHOTHUHANG.</summary>
    public string RequiredNote { get; set; } = "CHOXEMHANGKHONGTHU";

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

    // Tài khoản GHN tạo sau 22/09/2026 chỉ nhận địa chỉ mới 2 cấp (Tỉnh → Phường/Xã), nên dùng danh mục v3 và gửi tên chuẩn
    // kèm is_new_to_address = true; mã quận / phường cũ bị GHN từ chối.
    public Task<IReadOnlyList<ShippingProvince>> GetProvincesAsync(CancellationToken ct = default) =>
        Cached("v3:provinces", async () =>
        {
            var data = await SendAsync(HttpMethod.Get, "/shiip/public-api/v3/master-data/province/all?offset=0&limit=200", null, ct);
            return (IReadOnlyList<ShippingProvince>)data.EnumerateArray()
                .Where(IsActive)
                .Select(p => new ShippingProvince(p.GetProperty("_id").GetInt32(), p.GetProperty("name").GetString()!))
                .Where(p => !p.Name.StartsWith("Test", StringComparison.OrdinalIgnoreCase))
                .ToList();
        });

    public Task<IReadOnlyList<ShippingWard>> GetWardsAsync(int provinceId, CancellationToken ct = default) =>
        Cached($"v3:wards:{provinceId}", async () =>
        {
            // province_id sai thì GHN trả data = null (không báo lỗi) → coi như danh sách rỗng.
            var data = await SendAsync(HttpMethod.Get, $"/shiip/public-api/v3/master-data/ward/all-by-province-id?province_id={provinceId}&offset=0&limit=200", null, ct);
            if (data.ValueKind != JsonValueKind.Array)
                return (IReadOnlyList<ShippingWard>)[];
            return data.EnumerateArray()
                .Where(IsActive)
                .Select(w => new ShippingWard(w.GetProperty("_id").GetInt32().ToString(), provinceId, w.GetProperty("name").GetString()!))
                .Where(w => !w.Name.StartsWith("Test", StringComparison.OrdinalIgnoreCase))
                .ToList();
        });

    public async Task<ShippingFee> CalculateFeeAsync(ShippingFeeRequest request, CancellationToken ct = default)
    {
        // API tính phí chưa nhận địa chỉ mới 2 cấp → dùng "xem trước đơn" (preview), tính đúng như lúc tạo vận đơn thật.
        // Tên / SĐT chỉ để qua kiểm tra bắt buộc của GHN, không lưu ở đâu.
        var data = await SendAsync(HttpMethod.Post, "/shiip/public-api/v2/shipping-order/preview", new
        {
            payment_type_id = 1,
            required_note = _options.RequiredNote,
            to_name = "Khach hang",
            to_phone = "0900000000",
            to_address = request.ToWardName,
            to_ward_name = request.ToWardName,
            to_province_name = request.ToProvinceName,
            is_new_to_address = true,
            service_type_id = 2, // giao hàng nhẹ, dưới 20 kg
            weight = request.WeightGrams,
            length = _options.DefaultLengthCm,
            width = _options.DefaultWidthCm,
            height = _options.DefaultHeightCm,
            insurance_value = (int)Math.Min(request.InsuranceValue, 5_000_000m), // GHN chỉ bảo hiểm tối đa 5 triệu
            items = new[] { new { name = "Hang hoa", quantity = 1, weight = request.WeightGrams } } // preview bắt buộc có tên hàng
        }, ct);
        return new ShippingFee(data.GetProperty("total_fee").GetDecimal());
    }

    public async Task<ShipmentCreated> CreateShipmentAsync(ShipmentRequest request, CancellationToken ct = default)
    {
        if (request.CodAmount > 50_000_000m)
            throw new UserFriendlyException("GHN chỉ thu hộ tối đa 50.000.000 đ cho một đơn.");

        var data = await SendAsync(HttpMethod.Post, "/shiip/public-api/v2/shipping-order/create", new
        {
            payment_type_id = 1, // shop trả cước cho GHN; phí ship của khách đã nằm trong tiền thu hộ (COD) hoặc đã thanh toán trước
            required_note = _options.RequiredNote,
            client_order_code = request.ClientOrderCode,
            to_name = request.ToName,
            to_phone = request.ToPhone,
            to_address = request.ToAddress,
            to_ward_name = request.ToWardName,
            to_province_name = request.ToProvinceName,
            is_new_to_address = true,
            cod_amount = (int)request.CodAmount,
            content = request.Content,
            note = request.Note,
            weight = request.WeightGrams,
            length = _options.DefaultLengthCm,
            width = _options.DefaultWidthCm,
            height = _options.DefaultHeightCm,
            insurance_value = (int)Math.Min(request.InsuranceValue, 5_000_000m),
            service_type_id = 2,
            items = request.Items.Select(i => new { name = i.Name, quantity = i.Quantity, price = (int)i.Price, weight = i.WeightGrams }).ToArray()
        }, ct, forStaff: true);

        var expected = data.TryGetProperty("expected_delivery_time", out var e) && e.ValueKind == JsonValueKind.String ? ParseTime(e.GetString()) : null;
        var total = data.TryGetProperty("total_fee", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetDecimal() : 0;
        return new ShipmentCreated(data.GetProperty("order_code").GetString()!, expected, total);
    }

    public async Task<ShipmentInfo> GetShipmentAsync(string orderCode, CancellationToken ct = default)
    {
        var data = await SendAsync(HttpMethod.Post, "/shiip/public-api/v2/shipping-order/detail", new { order_code = orderCode }, ct, forStaff: true);
        var status = data.GetProperty("status").GetString()!;
        DateTime? updated = data.TryGetProperty("updated_date", out var u) && u.ValueKind == JsonValueKind.String ? ParseTime(u.GetString()) : null;
        DateTime? expected = data.TryGetProperty("leadtime", out var l) && l.ValueKind == JsonValueKind.String ? ParseTime(l.GetString()) : null;
        return new ShipmentInfo(status, updated, expected);
    }

    public async Task CancelShipmentAsync(string orderCode, string reason, CancellationToken ct = default)
    {
        var data = await SendAsync(HttpMethod.Post, "/shiip/public-api/v2/switch-status/cancel", new
        {
            order_codes = new[] { orderCode },
            reason_code = "GHN-CANCEL-OTHER",
            reason
        }, ct, forStaff: true);

        // Mã hợp lệ nhưng đơn đã được lấy hàng → GHN trả result:false, không phải lỗi HTTP.
        var entry = data.EnumerateArray().FirstOrDefault();
        if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.False)
            throw new UserFriendlyException("GHN không cho hủy vận đơn này (đơn đã được lấy hàng hoặc đang vận chuyển).");
    }

    public async Task<string> GetPrintUrlAsync(string orderCode, CancellationToken ct = default)
    {
        var data = await SendAsync(HttpMethod.Post, "/shiip/public-api/v2/a5/gen-token", new { order_codes = new[] { orderCode } }, ct, forStaff: true);
        return $"{_options.BaseUrl.TrimEnd('/')}/a5/public-api/printA5?token={Uri.EscapeDataString(data.GetProperty("token").GetString()!)}";
    }

    // GHN trả thời gian ISO 8601 (UTC); hệ thống lưu giờ địa phương.
    private static DateTime? ParseTime(string? value) =>
        DateTimeOffset.TryParse(value, out var t) ? t.LocalDateTime : null;

    // Danh mục mới: status 1 = đang dùng, 2 = ngưng, 10 = đã xóa — chỉ đưa mục đang dùng cho khách chọn.
    private static bool IsActive(JsonElement e) =>
        !e.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.Number || status.GetInt32() == 1;

    private async Task<T> Cached<T>(string key, Func<Task<T>> load)
    {
        if (_cache.TryGetValue($"ghn:{key}", out T? hit) && hit is not null)
            return hit;
        var value = await load();
        _cache.Set($"ghn:{key}", value, MasterDataCache);
        return value;
    }

    /// <summary>Gọi GHN, kiểm tra mã trả về và trả phần "data". Lỗi GHN ghi vào log, người dùng chỉ thấy thông báo chung.</summary>
    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct, bool forStaff = false)
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
                throw new UserFriendlyException(forStaff ? StaffMessage(text) : FriendlyMessage(text));
            }

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var code) && code.GetInt32() != 200)
            {
                _logger.LogWarning("GHN {Method} {Path} code {Code}: {Body}", method, path, code.GetInt32(), Truncate(text));
                throw new UserFriendlyException(forStaff ? StaffMessage(text) : FriendlyMessage(text));
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

    // Dành cho nhân viên / quản trị: hiện nguyên lý do GHN từ chối (VD "không lấy được thông tin kho", "số điện thoại không hợp lệ")
    // để biết sửa gì; vẫn là lỗi nghiệp vụ nên không ảnh hưởng khách.
    private static string StaffMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? Get(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString() : null;
            var detail = Get("message") ?? Get("code_message_value");
            if (detail != null)
                return "GHN từ chối: " + detail;
        }
        catch (JsonException) { }
        return "GHN từ chối yêu cầu. Vui lòng kiểm tra lại thông tin đơn.";
    }

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500];
}

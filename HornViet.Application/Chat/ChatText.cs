using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HornViet.Application.Chat;

/// <summary>
/// Xử lý câu chữ cho chatbot: chuẩn hóa trước khi huấn luyện / đoán ý định, và che thông tin cá nhân.
/// Câu mẫu lúc huấn luyện và câu khách gõ lúc dùng đều đi qua cùng <see cref="Normalize"/>, nên "Lược sừng bn tiền",
/// "luoc sung bao nhieu tien" và "LƯỢC SỪNG BAO NHIÊU TIỀN???" là cùng một câu với model.
/// </summary>
public static partial class ChatText
{
    // Từ viết tắt / teencode hay gặp khi chat (so khớp sau khi đã bỏ dấu). Chỉ đổi khi cả từ khớp,
    // không đụng tới "150k" hay mã đơn "HSV000123". Bỏ qua các từ một chữ cái dễ hiểu nhầm (a, e, c).
    private static readonly Dictionary<string, string> Slang = new()
    {
        ["k"] = "khong", ["ko"] = "khong", ["kh"] = "khong", ["khg"] = "khong", ["kg"] = "khong", ["hok"] = "khong",
        ["hong"] = "khong", ["hem"] = "khong", ["khum"] = "khong", ["hk"] = "khong",
        ["bn"] = "bao nhieu", ["bnh"] = "bao nhieu", ["bnhieu"] = "bao nhieu", ["baonhieu"] = "bao nhieu",
        ["sp"] = "san pham", ["sanpham"] = "san pham",
        ["dc"] = "duoc", ["dk"] = "duoc",
        ["j"] = "gi", ["ji"] = "gi",
        ["vs"] = "voi",
        ["ntn"] = "nhu the nao",
        ["sdt"] = "so dien thoai", ["dt"] = "dien thoai",
        ["ck"] = "chuyen khoan",
        ["tk"] = "tai khoan",
        ["mk"] = "minh", ["mik"] = "minh", ["mh"] = "minh",
        ["ad"] = "shop", ["sop"] = "shop",
        ["ib"] = "nhan tin", ["inbox"] = "nhan tin",
        ["nv"] = "nhan vien", ["tv"] = "tu van",
        ["tks"] = "cam on", ["thanks"] = "cam on", ["thank"] = "cam on", ["camon"] = "cam on", ["thankyou"] = "cam on",
        ["oke"] = "ok", ["okie"] = "ok", ["oki"] = "ok", ["okela"] = "ok",
        ["r"] = "roi", ["z"] = "vay", ["v"] = "vay", ["zay"] = "vay", ["dzay"] = "vay",
        ["hn"] = "ha noi", ["hcm"] = "ho chi minh", ["sg"] = "sai gon",
        ["sz"] = "size", ["fb"] = "facebook", ["zl"] = "zalo"
    };

    /// <summary>
    /// Bỏ dấu, chữ thường, chỉ giữ chữ và số, gộp ký tự lặp ("shoppp" → "shop"), đổi teencode thành từ đầy đủ.
    /// </summary>
    public static string Normalize(string text) =>
        string.Join(' ', Words(text).SelectMany(w => Slang.TryGetValue(w, out var full) ? full.Split(' ') : [w]));

    /// <summary>Chỉ bỏ dấu và ký tự đặc biệt (không đổi teencode) — dùng cho tên sản phẩm, danh mục.</summary>
    public static string Fold(string text) => string.Join(' ', Words(text));

    /// <summary>Các từ của câu đã chuẩn hóa.</summary>
    public static string[] Tokens(string text) => Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Che số điện thoại, email và dãy số dài (số tài khoản, số thẻ) trước khi đưa câu của khách vào dữ liệu huấn luyện.
    /// </summary>
    public static string MaskPersonalInfo(string text)
    {
        var masked = EmailRegex().Replace(text, "[email]");
        masked = PhoneRegex().Replace(masked, "[số điện thoại]");
        return LongNumberRegex().Replace(masked, "[số]");
    }

    private static IEnumerable<string> Words(string text)
    {
        var lower = text.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(lower.Length);
        foreach (var ch in lower)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(ch is >= 'a' and <= 'z' or >= '0' and <= '9' ? ch : ' ');
        }
        var collapsed = RepeatedCharRegex().Replace(sb.ToString(), "$1");
        return collapsed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    [GeneratedRegex(@"([a-z])\1{2,}")]
    private static partial Regex RepeatedCharRegex();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailRegex();

    // 0912345678, 0912 345 678, 0912.345.678, +84912345678
    [GeneratedRegex(@"(?<!\d)(?:\+?84|0)(?:[\s.-]?\d){8,10}(?!\d)")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"(?<!\d)\d{9,}(?!\d)")]
    private static partial Regex LongNumberRegex();
}

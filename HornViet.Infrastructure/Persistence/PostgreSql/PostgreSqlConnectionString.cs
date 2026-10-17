using Npgsql;

namespace HornViet.Infrastructure.Persistence.PostgreSql;

/// <summary>
/// Chuẩn hóa chuỗi kết nối PostgreSQL. Neon / Supabase hiển thị dạng URI:
/// postgresql://user:matkhau@host/tencsdl?sslmode=require — Npgsql chỉ đọc dạng "Host=...;Database=...;Username=...",
/// nên URI được chuyển sang dạng đó; chuỗi đã đúng dạng giữ nguyên các tham số.
/// </summary>
public static class PostgreSqlConnectionString
{
    public static string Normalize(string value)
    {
        var builder = IsUri(value) ? FromUri(value) : new NpgsqlConnectionStringBuilder(value);

        // Hệ thống không dùng Kerberos: tắt thương lượng mã hóa GSS để Npgsql không thử nạp thư viện libgssapi
        // (không có trong image Docker) — tránh dòng lỗi "Cannot load library libgssapi_krb5.so.2" mỗi lần kết nối.
        builder.GssEncryptionMode = GssEncryptionMode.Disable;
        return builder.ConnectionString;
    }

    private static bool IsUri(string value) =>
        value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    private static NpgsqlConnectionStringBuilder FromUri(string value)
    {
        var uri = new Uri(value);
        var user = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(user[0]),
            Password = user.Length > 1 ? Uri.UnescapeDataString(user[1]) : null,
            SslMode = SslMode.Require // dịch vụ cloud luôn yêu cầu mã hóa đường truyền
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]).ToLowerInvariant();
            var val = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            if (key == "sslmode")
                builder.SslMode = Enum.Parse<SslMode>(val.Replace("-", ""), ignoreCase: true); // require, verify-full...
            else if (key == "channel_binding")
                builder.ChannelBinding = Enum.Parse<ChannelBinding>(val, ignoreCase: true);
        }

        return builder;
    }
}

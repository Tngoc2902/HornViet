using Npgsql;

namespace HornViet.Infrastructure.Persistence.PostgreSql;

/// <summary>
/// Neon / Supabase hiển thị chuỗi kết nối dạng URI: postgresql://user:matkhau@host/tencsdl?sslmode=require
/// — Npgsql chỉ đọc dạng "Host=...;Database=...;Username=...". Chuyển URI sang dạng đó; chuỗi đã đúng dạng giữ nguyên.
/// </summary>
public static class PostgreSqlConnectionString
{
    public static string Normalize(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

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

        return builder.ConnectionString;
    }
}

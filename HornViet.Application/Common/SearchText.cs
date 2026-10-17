namespace HornViet.Application.Common;

/// <summary>
/// Dùng TRONG truy vấn LINQ để tìm kiếm không phân biệt hoa thường và dấu: khách gõ "luoc sung" vẫn ra "Lược sừng".
/// EF Core dịch hàm này sang SQL tùy CSDL (đăng ký trong HornVietDbContext):
/// - SQL Server: giữ nguyên cột — collation Latin1_General_100_CI_AI vốn đã không phân biệt hoa thường và dấu;
/// - PostgreSQL: lower(unaccent(...)) — bỏ dấu (kể cả ư, ơ, đ) rồi chuyển chữ thường.
/// Áp dụng cho cả hai vế: <c>SearchText.Fold(p.Name).Contains(SearchText.Fold(keyword))</c>.
/// </summary>
public static class SearchText
{
    public static string Fold(string value) =>
        throw new NotSupportedException("SearchText.Fold chỉ dùng trong truy vấn LINQ gửi xuống CSDL.");
}

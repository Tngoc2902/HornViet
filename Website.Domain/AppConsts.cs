namespace Website.Domain;

public static class AppConsts
{
    /// <summary>Biến thể có tồn kho &lt;= ngưỡng này được gắn nhãn "Sắp hết hàng" ở trang quản trị.</summary>
    public const int LowStockThreshold = 5;

    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 100;

    /// <summary>Số di động Việt Nam: 0xxxxxxxxx hoặc +84xxxxxxxxx.</summary>
    public const string PhoneRegex = @"^(0|\+84)(3|5|7|8|9)\d{8}$";
}

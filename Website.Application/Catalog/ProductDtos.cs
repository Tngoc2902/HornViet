using System.ComponentModel.DataAnnotations;
using Website.Application.Common;

namespace Website.Application.Catalog;

public enum ProductSorting
{
    Newest = 0,
    PriceAsc = 1,
    PriceDesc = 2,
    NameAsc = 3,
    BestSelling = 4
}

/// <summary>Tìm kiếm & lọc sản phẩm phía khách hàng (UC 3.2.2.5).</summary>
public class GetProductsInput : PagedInput
{
    public string? Keyword { get; set; }

    /// <summary>Nhiều danh mục: ?categoryIds=1&amp;categoryIds=2</summary>
    public List<int>? CategoryIds { get; set; }

    public List<int>? MaterialIds { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MinPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MaxPrice { get; set; }

    public bool InStockOnly { get; set; }

    public ProductSorting Sorting { get; set; } = ProductSorting.Newest;
}

public class ProductListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public string MainImage { get; set; } = null!;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = null!;
    public int TotalStock { get; set; }
    public bool InStock { get; set; }
    public DateTime CreationTime { get; set; }
}

public class ProductVariantDto
{
    public int Id { get; set; }
    public string? Size { get; set; }
    public string? Style { get; set; }
    public string DisplayName { get; set; } = null!;
    public int StockQuantity { get; set; }
    public bool InStock { get; set; }
    public bool IsLowStock { get; set; }
}

public class ProductMaterialDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Origin { get; set; }
    public string? Description { get; set; }
}

/// <summary>Chi tiết sản phẩm: bộ ảnh, biến thể + tồn kho, nguồn gốc nguyên liệu (UC 3.2.2.6).</summary>
public class ProductDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public string MainImage { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public ProductMaterialDto Material { get; set; } = null!;
    public IReadOnlyList<string> Images { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ProductVariantDto> Variants { get; set; } = Array.Empty<ProductVariantDto>();
    public int TotalStock { get; set; }
    public DateTime CreationTime { get; set; }
}

/// <summary>Danh sách sản phẩm ở trang quản trị (Hình 3.24).</summary>
public class GetProductsForAdminInput : PagedInput
{
    public string? Keyword { get; set; }
    public int? CategoryId { get; set; }
    public bool? IsActive { get; set; }
    public bool LowStockOnly { get; set; }
}

public class ProductAdminListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string MainImage { get; set; } = null!;
    public decimal Price { get; set; }
    public string CategoryName { get; set; } = null!;
    public string MaterialName { get; set; } = null!;
    public int VariantCount { get; set; }
    public int TotalStock { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreationTime { get; set; }
}

public class ProductVariantInput
{
    /// <summary>Có Id = sửa biến thể cũ; bỏ trống = thêm mới. Biến thể cũ không gửi lên sẽ bị xóa mềm.</summary>
    public int? Id { get; set; }

    [StringLength(100)]
    public string? Size { get; set; }

    [StringLength(100)]
    public string? Style { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Tồn kho không được âm.")]
    public int StockQuantity { get; set; }
}

public class CreateOrUpdateProductInput
{
    /// <summary>Bỏ trống khi tạo mới.</summary>
    public int? Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục.")]
    public int CategoryId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nguyên liệu.")]
    public int MaterialId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm."), StringLength(255)]
    public string Name { get; set; } = null!;

    [Range(0, 999_999_999, ErrorMessage = "Giá bán không hợp lệ.")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ảnh đại diện."), StringLength(500)]
    public string MainImage { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Bắt buộc ít nhất một biến thể.</summary>
    public List<ProductVariantInput> Variants { get; set; } = new();

    /// <summary>Bộ ảnh chi tiết (URL trả về từ API upload ảnh), theo thứ tự hiển thị.</summary>
    public List<string> Images { get; set; } = new();
}

public class SetProductActiveInput
{
    public int Id { get; set; }
    public bool IsActive { get; set; }
}

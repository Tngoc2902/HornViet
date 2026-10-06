using Website.Domain.SharedKernel;

namespace Website.Domain.Catalog.Entities;

public class Category : Entity<int>, IHasCreationTime, ISoftDelete
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreationTime { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>Nguyên liệu và nguồn gốc (VD: Sừng trâu — làng nghề Thụy Ứng).</summary>
public class Material : Entity<int>, IHasCreationTime, ISoftDelete
{
    public string Name { get; set; } = null!;
    public string? Origin { get; set; }
    public string? Description { get; set; }
    public DateTime CreationTime { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>Aggregate root: sản phẩm sở hữu các biến thể và bộ ảnh của nó.</summary>
public class Product : Entity<int>, IHasCreationTime, ISoftDelete
{
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public int MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public string MainImage { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>false = tạm ẩn khỏi cửa hàng nhưng vẫn quản lý được ở trang quản trị.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreationTime { get; set; }
    public bool IsDeleted { get; set; }

    public List<ProductVariant> Variants { get; set; } = new();
    public List<ProductImage> Images { get; set; } = new();
}

/// <summary>
/// Biến thể (kích thước / kiểu dáng) — đơn vị quản lý tồn kho và đặt mua.
/// Tồn kho chỉ thay đổi qua các phương thức bên dưới để luôn kiểm tra quy tắc nghiệp vụ.
/// </summary>
public class ProductVariant : Entity<int>, ISoftDelete
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string? Size { get; set; }
    public string? Style { get; set; }

    public int StockQuantity { get; private set; }

    /// <summary>
    /// Khóa lạc quan (rowversion): hai khách cùng mua một biến thể tồn kho thấp thì giao dịch đến sau
    /// bị từ chối thay vì bán vượt tồn kho.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public ProductVariant() { }

    public ProductVariant(string? size, string? style, int stockQuantity)
    {
        Size = size;
        Style = style;
        SetStock(stockQuantity);
    }

    public string DisplayName
    {
        get
        {
            var name = string.Join(" / ", new[] { Size, Style }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return name.Length == 0 ? "Mặc định" : name;
        }
    }

    public void DecreaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new UserFriendlyException("Số lượng phải lớn hơn 0.");
        if (quantity > StockQuantity)
            throw new UserFriendlyException(
                $"Biến thể \"{DisplayName}\" chỉ còn {StockQuantity} sản phẩm, không đủ số lượng yêu cầu.");
        StockQuantity -= quantity;
    }

    public void IncreaseStock(int quantity)
    {
        if (quantity <= 0) return;
        StockQuantity += quantity;
    }

    /// <summary>Nhân viên điều chỉnh tồn kho (kiểm kê, nhập hàng).</summary>
    public void SetStock(int quantity)
    {
        if (quantity < 0)
            throw new UserFriendlyException("Tồn kho không được âm.");
        StockQuantity = quantity;
    }
}

public class ProductImage : Entity<int>
{
    public int ProductId { get; set; }
    public string ImageUrl { get; set; } = null!;
    public int DisplayOrder { get; set; }
}

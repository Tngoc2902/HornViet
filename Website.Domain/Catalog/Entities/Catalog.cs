using Website.Domain.SharedKernel;

// File thuộc tầng Domain, nhóm Catalog (Danh mục hàng hóa): gồm các thực thể Category (danh mục),
// Material (nguyên liệu), Product (sản phẩm), ProductVariant (biến thể) và ProductImage (ảnh sản phẩm).
namespace Website.Domain.Catalog.Entities;

/// <summary>
/// Danh mục sản phẩm (VD: Lược sừng, Trâm cài, Đồ trang trí) — thực thể (Entity) có khóa chính kiểu int,
/// dùng để phân loại và lọc sản phẩm trên cửa hàng.
/// </summary>
// Cú pháp "class Category : Entity<int>, IHasCreationTime, ISoftDelete": kế thừa lớp cơ sở Entity<int> (có Id)
// và cài đặt hai interface — được tự động gán thời điểm tạo và hỗ trợ xóa mềm (danh mục bị xóa vẫn còn trong
// CSDL để các sản phẩm cũ không bị mất tham chiếu).
public class Category : Entity<int>, IHasCreationTime, ISoftDelete
{
    /// <summary>Tên danh mục, bắt buộc.</summary>
    // null!: gán null nhưng dùng dấu ! để tắt cảnh báo nullable — báo cho trình biên dịch rằng giá trị thật sẽ
    // được gán sau (từ CSDL hoặc từ dữ liệu nhập), thuộc tính này không bao giờ null khi sử dụng.
    public string Name { get; set; } = null!;
    /// <summary>Mô tả danh mục, không bắt buộc (string? cho phép null).</summary>
    public string? Description { get; set; }
    /// <summary>Thời điểm tạo, do DbContext tự gán (IHasCreationTime).</summary>
    public DateTime CreationTime { get; set; }
    /// <summary>Cờ xóa mềm (ISoftDelete).</summary>
    public bool IsDeleted { get; set; }
}

// Thực thể (Entity) mô tả chất liệu làm nên sản phẩm; tách thành bảng riêng thay vì cột chữ trong Product
// để nhiều sản phẩm dùng chung, khách lọc sản phẩm theo nguyên liệu, và thể hiện nguồn gốc làng nghề.
// Cũng hỗ trợ tự gán thời điểm tạo và xóa mềm như Category.
/// <summary>Nguyên liệu và nguồn gốc (VD: Sừng trâu — làng nghề Thụy Ứng).</summary>
public class Material : Entity<int>, IHasCreationTime, ISoftDelete
{
    /// <summary>Tên nguyên liệu, VD "Sừng trâu", bắt buộc.</summary>
    public string Name { get; set; } = null!;
    /// <summary>Nguồn gốc / làng nghề, không bắt buộc.</summary>
    public string? Origin { get; set; }
    /// <summary>Mô tả thêm về nguyên liệu, không bắt buộc.</summary>
    public string? Description { get; set; }
    /// <summary>Thời điểm tạo, do DbContext tự gán.</summary>
    public DateTime CreationTime { get; set; }
    /// <summary>Cờ xóa mềm.</summary>
    public bool IsDeleted { get; set; }
}

// Aggregate Root (Gốc tổng hợp) trong DDD: thực thể "cửa ngõ" của một cụm đối tượng gắn bó với nhau
// (Product + các ProductVariant + các ProductImage). Bên ngoài thao tác với cụm này thông qua Product;
// khi lưu/xóa sản phẩm thì biến thể và ảnh đi kèm (cấu hình xóa dây chuyền Cascade trong DbContext).
// Liên quan UC Quản lý sản phẩm và biến thể (Bảng 3.10): quy tắc "sản phẩm phải có ít nhất một biến thể" được
// kiểm tra ở ProductAppService (tầng Application) khi tạo/sửa; xóa sản phẩm là xóa mềm (ISoftDelete) để lịch sử
// đơn hàng không bị ảnh hưởng.
/// <summary>Aggregate root: sản phẩm sở hữu các biến thể và bộ ảnh của nó.</summary>
public class Product : Entity<int>, IHasCreationTime, ISoftDelete
{
    /// <summary>Khóa ngoại tới danh mục (Category) của sản phẩm.</summary>
    public int CategoryId { get; set; }
    /// <summary>
    /// Thuộc tính điều hướng (navigation property) tới danh mục: EF Core nạp đối tượng Category khi truy vấn
    /// có Include, giúp đọc c.Category.Name mà không cần viết JOIN thủ công.
    /// </summary>
    public Category Category { get; set; } = null!;

    /// <summary>Khóa ngoại tới nguyên liệu (Material) của sản phẩm.</summary>
    public int MaterialId { get; set; }
    /// <summary>Thuộc tính điều hướng tới nguyên liệu.</summary>
    public Material Material { get; set; } = null!;

    /// <summary>Tên sản phẩm, bắt buộc.</summary>
    public string Name { get; set; } = null!;
    /// <summary>
    /// Giá bán hiện tại (VND). Giá đặt ở cấp sản phẩm nên mọi biến thể của cùng một sản phẩm có chung giá.
    /// Khi đặt hàng, giá này được sao chép vào OrderItem.UnitPrice (Bảng 3.8 – lưu đơn giá lúc mua).
    /// </summary>
    // decimal (không dùng double/float) vì là kiểu số thập phân chính xác, không sai số làm tròn — bắt buộc với tiền.
    public decimal Price { get; set; }
    /// <summary>Đường dẫn ảnh đại diện hiển thị ở danh sách sản phẩm, bắt buộc.</summary>
    public string MainImage { get; set; } = null!;
    /// <summary>Mô tả chi tiết sản phẩm, không bắt buộc.</summary>
    public string? Description { get; set; }

    // Khác với xóa mềm (IsDeleted): sản phẩm IsActive = false vẫn hiện ở trang quản trị và có thể bật lại;
    // khách không thêm vào giỏ hay đặt hàng được (CartAppService, OrderAppService kiểm tra cờ này).
    // "= true": giá trị mặc định khi tạo mới — sản phẩm mới mặc định đang bán.
    /// <summary>false = tạm ẩn khỏi cửa hàng nhưng vẫn quản lý được ở trang quản trị.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Thời điểm tạo, do DbContext tự gán.</summary>
    public DateTime CreationTime { get; set; }
    /// <summary>Cờ xóa mềm (Bảng 3.10 – xóa sản phẩm là xóa mềm).</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Danh sách biến thể (kích thước / kiểu dáng) của sản phẩm — quan hệ một – nhiều.</summary>
    // "= new()": biểu thức new rút gọn (target-typed new), tương đương new List<ProductVariant>();
    // khởi tạo sẵn danh sách rỗng để không bao giờ bị null khi gọi Variants.Add(...).
    public List<ProductVariant> Variants { get; set; } = new();
    /// <summary>Bộ ảnh phụ của sản phẩm, sắp xếp theo ProductImage.DisplayOrder.</summary>
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

using HornViet.Domain.SharedKernel;

namespace HornViet.Domain.Catalog.Entities;

/// <summary>
/// Bộ sưu tập: nhóm sản phẩm do nhân viên tự chọn (VD "Quà tặng Tết", "Trang sức sừng thủ công") — khác danh mục / chất liệu
/// ở chỗ một sản phẩm có thể nằm trong nhiều bộ sưu tập và thứ tự do người quản lý sắp xếp.
/// </summary>
public class Collection : Entity<int>, IHasCreationTime, ISoftDelete
{
    /// <summary>Tên hiển thị, VD "Quà tặng Tết 2027".</summary>
    public string Name { get; set; } = null!;

    /// <summary>Đường dẫn thân thiện trên trang khách: /bo-suu-tap/{Slug} (chữ thường, không dấu, nối bằng gạch ngang).</summary>
    public string Slug { get; set; } = null!;

    /// <summary>Câu giới thiệu ngắn hiện dưới tên bộ sưu tập.</summary>
    public string? Description { get; set; }

    /// <summary>Ảnh bìa (/uploads/... hoặc Cloudinary); để trống thì trang khách dùng ảnh sản phẩm đầu tiên.</summary>
    public string? CoverImage { get; set; }

    /// <summary>false = ẩn khỏi trang khách nhưng vẫn quản lý được trong trang quản trị.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Thứ tự hiển thị (nhỏ đứng trước).</summary>
    public int DisplayOrder { get; set; }

    public List<CollectionProduct> Items { get; set; } = new();

    public DateTime CreationTime { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>Sản phẩm thuộc một bộ sưu tập, kèm thứ tự hiển thị trong bộ.</summary>
public class CollectionProduct : Entity<int>
{
    public int CollectionId { get; set; }
    public int ProductId { get; set; }
    public int DisplayOrder { get; set; }
    public Product Product { get; set; } = null!;
}

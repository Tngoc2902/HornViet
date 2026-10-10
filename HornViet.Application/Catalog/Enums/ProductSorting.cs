namespace HornViet.Application.Catalog.Enums;

/// <summary>
/// Các kiểu sắp xếp danh sách sản phẩm mà khách chọn ở trang cửa hàng (ô "Sắp xếp theo").
/// Frontend gửi lên dạng số (?sorting=1) hoặc tên; ProductAppService.GetAllAsync dùng giá trị này
/// trong biểu thức switch để quyết định cách ORDER BY.
/// Dù chọn kiểu nào, sản phẩm còn hàng vẫn luôn được xếp trước (quy tắc của Bảng 3.5).
/// </summary>
// enum = kiểu liệt kê: một tập hằng số có tên; mỗi tên gắn với một số nguyên cố định (= 0, = 1, ...).
public enum ProductSorting
{
    /// <summary>Mới nhất trước (theo CreationTime giảm dần) — giá trị mặc định.</summary>
    Newest = 0,
    /// <summary>Giá tăng dần (rẻ trước).</summary>
    PriceAsc = 1,
    /// <summary>Giá giảm dần (đắt trước).</summary>
    PriceDesc = 2,
    /// <summary>Tên A → Z.</summary>
    NameAsc = 3,
    /// <summary>Bán chạy nhất: tổng số lượng đã bán trong các đơn hàng ở trạng thái Hoàn thành, giảm dần.</summary>
    BestSelling = 4
}

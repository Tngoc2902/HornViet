using System.ComponentModel.DataAnnotations;
using HornViet.Application.Common;
using HornViet.Application.Catalog.Enums;

namespace HornViet.Application.Catalog;

// File này chứa các DTO (Data Transfer Object) của chức năng Sản phẩm:
// - "Input" = dữ liệu frontend GỬI LÊN (tham số tìm kiếm, form thêm/sửa sản phẩm).
// - "Dto"   = dữ liệu backend TRẢ VỀ cho frontend (đã được "làm phẳng", chỉ gồm các trường giao diện cần).
// Không trả thẳng entity (Product, ProductVariant...) ra ngoài để: tránh lộ cột nội bộ (IsDeleted, RowVersion),
// tránh vòng lặp tham chiếu khi chuyển sang JSON, và để giao diện API không phụ thuộc vào cấu trúc bảng CSDL.
// Lưu ý: ASP.NET Core trả JSON theo kiểu camelCase, nên thuộc tính InStock sẽ thành "inStock" ở phía frontend.

/// <summary>Tìm kiếm & lọc sản phẩm phía khách hàng (UC 3.2.2.5).</summary>
// Input của Bảng 3.5 "Tìm kiếm & lọc sản phẩm": frontend gửi các tham số này trên query string của
// GET /api/services/app/Product/GetAll (controller dùng [FromQuery]). Mọi tiêu chí đều tùy chọn:
// tiêu chí nào để trống thì service không thêm điều kiện lọc tương ứng.
// ": PagedInput" = kế thừa lớp PagedInput nên có sẵn SkipCount (bỏ qua bao nhiêu dòng) và MaxResultCount
// (lấy tối đa bao nhiêu dòng, mặc định 12, tối đa 100) để phân trang.
// Các thuộc tính gắn [Range]... được ASP.NET Core tự kiểm tra (nhờ [ApiController]) TRƯỚC khi gọi service;
// sai thì trả HTTP 400 "Dữ liệu không hợp lệ." và service hoàn toàn không chạy.
public class GetProductsInput : PagedInput
{
    /// <summary>
    /// Từ khóa khách gõ vào ô tìm kiếm. Tìm trong tên sản phẩm, tên danh mục và tên chất liệu.
    /// Kiểu "string?" = chuỗi có thể null (khách không nhập gì).
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>Nhiều danh mục: ?categoryIds=1&amp;categoryIds=2</summary>
    // Danh sách Id danh mục khách tích chọn ở bộ lọc; sản phẩm thuộc MỘT trong các danh mục này là khớp.
    // Null hoặc rỗng = không lọc theo danh mục.
    public List<int>? CategoryIds { get; set; }

    /// <summary>
    /// Danh sách Id chất liệu (sừng trâu, sừng bò...) khách tích chọn; gửi giống CategoryIds: ?materialIds=1&amp;materialIds=3.
    /// Null hoặc rỗng = không lọc theo chất liệu.
    /// </summary>
    public List<int>? MaterialIds { get; set; }

    /// <summary>Chỉ lấy sản phẩm thuộc bộ sưu tập này (trang /bo-suu-tap/{slug}). Null = không lọc.</summary>
    public int? CollectionId { get; set; }

    /// <summary>
    /// Giá thấp nhất của khoảng giá. "decimal?" = có thể null (không nhập = không giới hạn dưới).
    /// [Range(0, double.MaxValue)]: không cho số âm; vi phạm → HTTP 400 trước khi service chạy.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? MinPrice { get; set; }

    /// <summary>
    /// Giá cao nhất của khoảng giá (null = không giới hạn trên). [Range] cũng chặn số âm → HTTP 400.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? MaxPrice { get; set; }

    /// <summary>
    /// true = chỉ lấy sản phẩm còn ít nhất một biến thể có tồn kho &gt; 0 (bộ lọc "còn hàng"). Mặc định false.
    /// </summary>
    public bool InStockOnly { get; set; }

    /// <summary>Kiểu sắp xếp khách chọn; không gửi thì mặc định là Newest (mới nhất trước).</summary>
    public ProductSorting Sorting { get; set; } = ProductSorting.Newest;
}

/// <summary>
/// DTO một "thẻ sản phẩm" trong danh sách kết quả tìm kiếm/lọc ở trang cửa hàng (Bảng 3.5).
/// Chỉ gồm các trường thẻ sản phẩm cần hiển thị (ảnh, tên, giá, nhãn còn/hết hàng), không kèm biến thể hay mô tả,
/// để câu SQL nhẹ và dữ liệu trả về nhỏ. Được tạo bằng phép chiếu Select trong ProductAppService.GetAllAsync.
/// </summary>
public class ProductListItemDto
{
    /// <summary>Mã sản phẩm — frontend dùng để mở trang chi tiết (gọi Product/Get?id=...).</summary>
    public int Id { get; set; }
    /// <summary>Tên sản phẩm hiển thị trên thẻ.</summary>
    // "= null!" chỉ để báo trình biên dịch "giá trị này chắc chắn sẽ được gán" (tắt cảnh báo nullable), không phải giá trị thật.
    public string Name { get; set; } = null!;
    /// <summary>Giá bán hiện tại (VNĐ). Kiểu decimal để tính tiền chính xác, không bị sai số như double.</summary>
    public decimal Price { get; set; }
    /// <summary>URL ảnh đại diện hiển thị trên thẻ sản phẩm.</summary>
    public string MainImage { get; set; } = null!;
    /// <summary>Ảnh thứ hai (khác ảnh đại diện) — thẻ sản phẩm đổi sang ảnh này khi rê chuột; null nếu sản phẩm chỉ có một ảnh.</summary>
    public string? HoverImage { get; set; }
    /// <summary>Id danh mục của sản phẩm.</summary>
    public int CategoryId { get; set; }
    /// <summary>Tên danh mục (lấy từ bảng Categories) để hiển thị kèm, frontend không phải gọi API thêm.</summary>
    public string CategoryName { get; set; } = null!;
    /// <summary>Id chất liệu của sản phẩm.</summary>
    public int MaterialId { get; set; }
    /// <summary>Tên chất liệu (VD: Sừng trâu) để hiển thị kèm.</summary>
    public string MaterialName { get; set; } = null!;
    /// <summary>Tổng tồn kho = cộng tồn kho của tất cả biến thể (chưa bị xóa) của sản phẩm.</summary>
    public int TotalStock { get; set; }
    /// <summary>
    /// true nếu còn ít nhất một biến thể có tồn kho &gt; 0. inStock = false → frontend hiện nhãn "Hết hàng".
    /// </summary>
    public bool InStock { get; set; }
    /// <summary>Thời điểm tạo sản phẩm (là tiêu chí của kiểu sắp xếp "Mới nhất").</summary>
    public DateTime CreationTime { get; set; }
}

/// <summary>
/// DTO một biến thể (kích thước / kiểu dáng) của sản phẩm, nằm trong ProductDetailDto.Variants.
/// Dùng ở trang chi tiết (Bảng 3.6: khách chọn biến thể; biến thể hết hàng bị làm mờ nút)
/// và ở form sửa sản phẩm trang quản trị (Bảng 3.10). Được tạo bởi ProductAppService.ToVariantDto.
/// </summary>
public class ProductVariantDto
{
    /// <summary>
    /// Mã biến thể. Khách thêm vào giỏ bằng Id này (AddCartItemInput.ProductVariantId);
    /// form quản trị gửi lại Id này khi sửa để backend biết là sửa biến thể cũ chứ không phải thêm mới.
    /// </summary>
    public int Id { get; set; }
    /// <summary>Kích thước (VD: "20 cm"); null nếu sản phẩm không chia theo kích thước.</summary>
    public string? Size { get; set; }
    /// <summary>Kiểu dáng (VD: "Khắc hoa văn"); null nếu không chia theo kiểu dáng.</summary>
    public string? Style { get; set; }
    /// <summary>Tên hiển thị ghép từ Size và Style ("20 cm / Khắc hoa văn"); cả hai trống thì là "Mặc định".</summary>
    public string DisplayName { get; set; } = null!;
    /// <summary>Ảnh của biến thể — trang chi tiết đổi sang ảnh này khi khách chọn biến thể; null = ảnh đại diện.</summary>
    public string? ImageUrl { get; set; }
    /// <summary>Số lượng còn trong kho của biến thể này.</summary>
    public int StockQuantity { get; set; }
    /// <summary>
    /// true khi tồn kho &gt; 0. inStock = false → frontend làm mờ / khóa nút chọn biến thể này (Bảng 3.6).
    /// </summary>
    public bool InStock { get; set; }
    /// <summary>
    /// true khi tồn kho &lt;= AppConsts.LowStockThreshold (= 5) → giao diện có thể gắn nhãn "Sắp hết hàng".
    /// </summary>
    public bool IsLowStock { get; set; }
}

/// <summary>
/// DTO thông tin chất liệu kèm nguồn gốc, lồng trong ProductDetailDto.Material để trang chi tiết
/// hiển thị mục "Nguồn gốc nguyên liệu" (VD: Sừng trâu — làng nghề Thụy Ứng).
/// </summary>
public class ProductMaterialDto
{
    /// <summary>Mã chất liệu.</summary>
    public int Id { get; set; }
    /// <summary>Tên chất liệu.</summary>
    public string Name { get; set; } = null!;
    /// <summary>Nguồn gốc (nơi sản xuất / làng nghề); có thể null.</summary>
    public string? Origin { get; set; }
    /// <summary>Mô tả thêm về chất liệu; có thể null.</summary>
    public string? Description { get; set; }
}

/// <summary>Chi tiết sản phẩm: bộ ảnh, biến thể + tồn kho, nguồn gốc nguyên liệu (UC 3.2.2.6).</summary>
// DTO trả về cho Bảng 3.6 "Xem chi tiết sản phẩm" (GET Product/Get?id=...). Cũng là kết quả trả về sau khi
// quản trị viên thêm/sửa sản phẩm (CreateAsync/UpdateAsync gọi lại GetAsync) để form hiển thị dữ liệu mới nhất.
public class ProductDetailDto
{
    /// <summary>Mã sản phẩm.</summary>
    public int Id { get; set; }
    /// <summary>Tên sản phẩm.</summary>
    public string Name { get; set; } = null!;
    /// <summary>
    /// Giá bán hiện tại. Giá đặt ở cấp sản phẩm: mọi biến thể của một sản phẩm có cùng giá.
    /// </summary>
    public decimal Price { get; set; }
    /// <summary>URL ảnh đại diện của sản phẩm (tách riêng với bộ ảnh chi tiết Images).</summary>
    public string MainImage { get; set; } = null!;
    /// <summary>Mô tả chi tiết sản phẩm; có thể null.</summary>
    public string? Description { get; set; }
    /// <summary>
    /// true = đang bán; false = đang ẩn. Khách không bao giờ nhận được sản phẩm ẩn (API trả 404),
    /// nên trường này chủ yếu dùng ở trang quản trị (hiển thị trạng thái Ẩn/Hiện).
    /// </summary>
    public bool IsActive { get; set; }
    /// <summary>Id danh mục (form sửa dùng để chọn sẵn danh mục).</summary>
    public int CategoryId { get; set; }
    /// <summary>Tên danh mục để hiển thị, lấy từ bảng Categories qua Include.</summary>
    public string CategoryName { get; set; } = null!;
    /// <summary>Chất liệu và nguồn gốc nguyên liệu (đối tượng lồng ProductMaterialDto).</summary>
    public ProductMaterialDto Material { get; set; } = null!;
    /// <summary>
    /// Bộ ảnh chi tiết (danh sách URL) đã sắp theo DisplayOrder. IReadOnlyList = danh sách chỉ đọc;
    /// mặc định là mảng rỗng (Array.Empty) thay vì null để frontend luôn duyệt được.
    /// </summary>
    public IReadOnlyList<string> Images { get; set; } = Array.Empty<string>();
    /// <summary>Các biến thể kèm tồn kho, sắp theo Id (thứ tự được tạo).</summary>
    public IReadOnlyList<ProductVariantDto> Variants { get; set; } = Array.Empty<ProductVariantDto>();
    /// <summary>Tổng tồn kho của mọi biến thể; bằng 0 → cả sản phẩm hết hàng.</summary>
    public int TotalStock { get; set; }
    /// <summary>Thời điểm tạo sản phẩm.</summary>
    public DateTime CreationTime { get; set; }
}

/// <summary>Danh sách sản phẩm ở trang quản trị (Hình 3.24).</summary>
// Input bộ lọc của màn hình danh sách sản phẩm phía quản trị (Bảng 3.10), gửi qua query string tới
// GET Product/GetAdminList (chỉ Nhân viên/Quản trị viên). Kế thừa PagedInput nên có SkipCount/MaxResultCount.
// Khác với GetProductsInput của khách: ở đây thấy cả sản phẩm đang ẩn và có thêm lọc "sắp hết hàng".
public class GetProductsForAdminInput : PagedInput
{
    /// <summary>Từ khóa tìm theo tên sản phẩm (chỉ tên, không tìm theo danh mục/chất liệu); null = không lọc.</summary>
    public string? Keyword { get; set; }
    /// <summary>Lọc theo một danh mục. "int?" = có thể null; null = tất cả danh mục.</summary>
    public int? CategoryId { get; set; }
    /// <summary>
    /// Lọc theo trạng thái: true = chỉ sản phẩm đang bán, false = chỉ sản phẩm đang ẩn, null = cả hai.
    /// Dùng "bool?" (3 trạng thái) để phân biệt được "không lọc" với "lọc false".
    /// </summary>
    public bool? IsActive { get; set; }
    /// <summary>
    /// true = chỉ lấy sản phẩm có ít nhất một biến thể tồn kho &lt;= 5 (AppConsts.LowStockThreshold), giúp nhân viên
    /// biết cần nhập thêm hàng.
    /// </summary>
    public bool LowStockOnly { get; set; }
}

/// <summary>
/// DTO một dòng trong bảng danh sách sản phẩm ở trang quản trị (Bảng 3.10).
/// Có thêm các cột phục vụ quản lý: số biến thể, cảnh báo sắp hết hàng, trạng thái Ẩn/Hiện.
/// </summary>
public class ProductAdminListItemDto
{
    /// <summary>Mã sản phẩm (dùng cho nút Sửa / Ẩn-Hiện / Xóa).</summary>
    public int Id { get; set; }
    /// <summary>Tên sản phẩm.</summary>
    public string Name { get; set; } = null!;
    /// <summary>URL ảnh đại diện (ảnh thu nhỏ trong bảng).</summary>
    public string MainImage { get; set; } = null!;
    /// <summary>Giá bán hiện tại.</summary>
    public decimal Price { get; set; }
    /// <summary>Tên danh mục.</summary>
    public string CategoryName { get; set; } = null!;
    /// <summary>Tên chất liệu.</summary>
    public string MaterialName { get; set; } = null!;
    /// <summary>Số biến thể đang có (không tính biến thể đã xóa mềm).</summary>
    public int VariantCount { get; set; }
    /// <summary>Tổng tồn kho của các biến thể.</summary>
    public int TotalStock { get; set; }
    /// <summary>true nếu có ít nhất một biến thể tồn kho &lt;= 5 → hiện cảnh báo "Sắp hết hàng".</summary>
    public bool IsLowStock { get; set; }
    /// <summary>true = đang bán, false = đang ẩn khỏi cửa hàng.</summary>
    public bool IsActive { get; set; }
    /// <summary>Thời điểm tạo (danh sách quản trị sắp xếp mới nhất trước theo trường này).</summary>
    public DateTime CreationTime { get; set; }
}

/// <summary>
/// Dữ liệu một dòng biến thể trong form thêm/sửa sản phẩm ở trang quản trị (Bảng 3.10),
/// nằm trong danh sách CreateOrUpdateProductInput.Variants.
/// Các thuộc tính gắn [StringLength]/[Range] được ASP.NET Core kiểm tra tự động (kể cả khi lồng trong danh sách);
/// vi phạm → HTTP 400 trước khi service chạy.
/// </summary>
public class ProductVariantInput
{
    /// <summary>Có Id = sửa biến thể cũ; bỏ trống = thêm mới. Biến thể cũ không gửi lên sẽ bị xóa mềm.</summary>
    // Id phải thuộc đúng sản phẩm đang sửa, nếu không UpdateAsync báo lỗi "Biến thể #... không thuộc sản phẩm này".
    public int? Id { get; set; }

    /// <summary>
    /// Kích thước; có thể bỏ trống. [StringLength(100)]: tối đa 100 ký tự (khớp độ dài cột trong CSDL).
    /// </summary>
    [StringLength(100)]
    public string? Size { get; set; }

    /// <summary>Kiểu dáng; có thể bỏ trống. [StringLength(100)]: tối đa 100 ký tự.</summary>
    // Không được có hai biến thể trùng cả (Size, Style) — kiểm tra trong ProductAppService.ValidateAsync.
    [StringLength(100)]
    public string? Style { get; set; }

    /// <summary>Ảnh của biến thể — bắt buộc, phải là ảnh đại diện hoặc một ảnh trong bộ ảnh của sản phẩm.</summary>
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Tồn kho của biến thể. [Range(0, int.MaxValue)]: không được âm → sai thì HTTP 400 "Tồn kho không được âm.".
    /// (Entity ProductVariant.SetStock cũng kiểm tra lại quy tắc này ở tầng Domain.)
    /// </summary>
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

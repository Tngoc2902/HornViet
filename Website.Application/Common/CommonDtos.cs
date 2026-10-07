// Tệp này chứa các lớp DTO dùng chung cho mọi Application Service (phân trang, danh sách, Id)
// và hàm mở rộng PageBy để phân trang truy vấn LINQ.
// System.ComponentModel.DataAnnotations: chứa các thuộc tính kiểm tra dữ liệu như [Range] dùng ở PagedInput.
using System.ComponentModel.DataAnnotations;
// Website.Domain: chứa AppConsts (kích thước trang mặc định / tối đa).
using Website.Domain;

namespace Website.Application.Common;

// DTO (Data Transfer Object) = đối tượng chỉ chứa dữ liệu, dùng để truyền kết quả từ API về frontend.
// PagedResultDto<T> là lớp generic (kiểu tổng quát): T là kiểu của từng phần tử, ví dụ PagedResultDto<UserDto>
// là một trang danh sách người dùng. Nhờ generic, một lớp dùng chung được cho mọi màn hình danh sách có phân trang
// (người dùng, sản phẩm, đơn hàng...). Frontend dùng TotalCount để vẽ thanh phân trang
// (số trang = TotalCount / số dòng mỗi trang, làm tròn lên), còn Items là các dòng của trang đang xem.
/// <summary>Kết quả phân trang theo định dạng ABP: { totalCount, items }.</summary>
public class PagedResultDto<T>
{
    /// <summary>
    /// Tổng số bản ghi thỏa điều kiện lọc trên toàn bộ CSDL (không phải chỉ số dòng của trang hiện tại).
    /// </summary>
    public int TotalCount { get; set; }
    /// <summary>Các bản ghi thuộc trang hiện tại mà frontend hiển thị.</summary>
    // IReadOnlyList: danh sách chỉ đọc — bên nhận chỉ duyệt/đọc, không thêm bớt phần tử.
    // Array.Empty<T>() trả về một mảng rỗng dùng chung (không cấp phát mới); gán làm giá trị mặc định
    // để Items không bao giờ là null, frontend luôn nhận được [] thay vì null.
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>
    /// Hàm khởi tạo không tham số: cần cho bộ chuyển đổi JSON và cho cú pháp khởi tạo
    /// <c>new PagedResultDto { ... }</c>.
    /// </summary>
    public PagedResultDto() { }

    /// <summary>Tạo kết quả phân trang từ tổng số bản ghi và danh sách của trang hiện tại.</summary>
    /// <param name="totalCount">Tổng số bản ghi thỏa điều kiện lọc (thường lấy bằng CountAsync).</param>
    /// <param name="items">Danh sách bản ghi của trang hiện tại (đã Skip/Take).</param>
    public PagedResultDto(int totalCount, IReadOnlyList<T> items)
    {
        // Gán hai giá trị vào thuộc tính để trả về frontend dưới dạng JSON { totalCount, items }.
        TotalCount = totalCount;
        Items = items;
    }
}

/// <summary>
/// DTO trả về một danh sách KHÔNG phân trang theo định dạng ABP: { items }.
/// Dùng khi danh sách luôn ngắn, ví dụ sổ địa chỉ của khách (Bảng 3.4) hay danh sách vai trò (Bảng 3.13).
/// </summary>
public class ListResultDto<T>
{
    /// <summary>Các phần tử của danh sách; mặc định là mảng rỗng để không bao giờ trả về null.</summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>Hàm khởi tạo không tham số, cần cho bộ chuyển đổi JSON.</summary>
    public ListResultDto() { }

    /// <summary>Tạo kết quả từ một danh sách có sẵn.</summary>
    /// <param name="items">Danh sách phần tử cần trả về frontend.</param>
    // Dấu "=>" ở đây là thân hàm dạng biểu thức (expression-bodied): viết gọn cho thân chỉ có một câu lệnh,
    // tương đương { Items = items; }.
    public ListResultDto(IReadOnlyList<T> items) => Items = items;
}

// Đây là lớp Input (dữ liệu frontend gửi lên). Các API danh sách có phân trang (ví dụ GetUsersInput)
// kế thừa lớp này nên tự có hai tham số skipCount / maxResultCount trên query string.
// Ví dụ frontend đang ở trang 3, mỗi trang 12 dòng: skipCount = (3 - 1) * 12 = 24, maxResultCount = 12.
/// <summary>Tham số phân trang theo định dạng ABP: skipCount / maxResultCount.</summary>
public class PagedInput
{
    /// <summary>
    /// Số bản ghi cần bỏ qua (tính từ đầu danh sách). [Range(0, int.MaxValue)] bắt buộc không âm;
    /// ASP.NET Core tự kiểm tra và trả HTTP 400 trước khi vào service nếu vi phạm.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int SkipCount { get; set; }

    /// <summary>
    /// Số bản ghi tối đa của một trang. [Range(1, AppConsts.MaxPageSize)] giới hạn từ 1 đến 100 dòng
    /// để frontend không thể yêu cầu một trang quá lớn làm nặng máy chủ (vi phạm → HTTP 400 tự động).
    /// Mặc định 12 dòng (AppConsts.DefaultPageSize) nếu frontend không gửi.
    /// </summary>
    [Range(1, AppConsts.MaxPageSize)]
    public int MaxResultCount { get; set; } = AppConsts.DefaultPageSize;
}

/// <summary>
/// DTO chỉ chứa một Id, dùng khi frontend chỉ cần gửi lên Id của đối tượng, ví dụ { "id": 5 } khi hủy đơn hàng.
/// TKey là tham số kiểu generic: kiểu của khóa chính (int, long...).
/// </summary>
public class EntityDto<TKey>
{
    /// <summary>Khóa chính của đối tượng cần thao tác.</summary>
    // default! : "default" là giá trị mặc định của kiểu TKey (0 với int/long); dấu "!" (null-forgiving)
    // báo trình biên dịch bỏ qua cảnh báo có thể null vì giá trị thật sẽ được gán từ JSON gửi lên.
    public TKey Id { get; set; } = default!;
}

/// <summary>
/// Các phương thức mở rộng (extension method) cho truy vấn LINQ IQueryable.
/// Lớp chứa extension method bắt buộc phải là static.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Phân trang một truy vấn: bỏ qua SkipCount bản ghi đầu và lấy tối đa MaxResultCount bản ghi.
    /// Từ khóa "this" ở tham số đầu biến hàm này thành extension method nên có thể gọi
    /// <c>query.PageBy(input)</c> như thể là phương thức có sẵn của IQueryable.
    /// </summary>
    /// <param name="query">Truy vấn gốc, phải đã được sắp xếp (OrderBy) để thứ tự giữa các trang ổn định.</param>
    /// <param name="input">Tham số phân trang frontend gửi lên.</param>
    /// <returns>Truy vấn mới đã gắn Skip/Take; chưa chạy xuống CSDL cho đến khi gọi ToListAsync.</returns>
    // Vì vẫn là IQueryable nên EF Core dịch Skip/Take thành SQL "OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY":
    // việc cắt trang diễn ra ngay trong SQL Server, chỉ các dòng của trang hiện tại được tải về ứng dụng.
    public static IQueryable<T> PageBy<T>(this IQueryable<T> query, PagedInput input) =>
        query.Skip(input.SkipCount).Take(input.MaxResultCount);
}

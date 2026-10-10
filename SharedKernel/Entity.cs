// File thuộc dự án SharedKernel (Nhân dùng chung – khái niệm Shared Kernel của DDD): nơi chứa các khối dựng
// nền tảng mà tầng Domain tham chiếu tới, gồm lớp cơ sở Entity, ValueObject, các interface đánh dấu
// (IHasCreationTime, ISoftDelete) và các lớp lỗi nghiệp vụ. Dự án này không phụ thuộc vào EF Core hay ASP.NET,
// nhờ vậy tầng Domain giữ được sự "thuần" nghiệp vụ, dễ kiểm thử đơn vị.
// Namespace đặt là HornViet.Domain.SharedKernel để các lớp Domain dùng như một phần tự nhiên của Domain.
namespace HornViet.Domain.SharedKernel;

// Entity (Thực thể) trong DDD: đối tượng được nhận diện bằng định danh (Id) chứ không phải bằng giá trị
// các thuộc tính. VD: hai sản phẩm trùng tên, trùng giá vẫn là hai sản phẩm khác nhau vì Id khác nhau.
// Mọi thực thể của hệ thống (Product, ProductVariant, Cart, Order, User...) đều kế thừa lớp này.
// Từ khóa abstract: lớp trừu tượng, không thể tạo trực tiếp bằng new, chỉ dùng làm lớp cha để kế thừa.
// Generic <TKey>: kiểu dữ liệu của khóa chính do lớp con tự chọn, VD Entity<int> cho Product,
// Entity<long> cho User — viết một lần, dùng lại cho nhiều kiểu khóa khác nhau.
/// <summary>Lớp cơ sở cho mọi thực thể có khóa chính kiểu <typeparamref name="TKey"/>.</summary>
public abstract class Entity<TKey>
{
    /// <summary>
    /// Khóa chính của thực thể. Với bản ghi mới, giá trị do SQL Server tự sinh (cột IDENTITY) khi lưu xuống CSDL.
    /// </summary>
    // { get; set; }: thuộc tính tự động (auto-property) — C# tự tạo trường ẩn để lưu giá trị.
    // default!: gán giá trị mặc định của TKey (VD 0 với int/long); dấu ! (null-forgiving) báo trình biên dịch
    // bỏ qua cảnh báo nullable, vì ta biết chắc Id sẽ có giá trị thật sau khi bản ghi được lưu.
    public TKey Id { get; set; } = default!;
}

// interface: "hợp đồng" chỉ khai báo thành viên mà lớp phải có, không chứa cài đặt.
// Đây là interface đánh dấu (marker): lớp nào cài đặt nó thì HornVietDbContext.ApplyAuditConcepts (tầng
// Infrastructure) tự gán CreationTime = DateTime.Now khi bản ghi được thêm mới và CreationTime chưa có giá trị.
// Nhờ vậy nghiệp vụ không phải tự nhớ gán thời điểm tạo — dùng để sắp xếp, lọc đơn theo ngày, thống kê.
/// <summary>Thời điểm tạo được DbContext gán tự động khi thêm mới.</summary>
public interface IHasCreationTime
{
    /// <summary>Thời điểm bản ghi được tạo (giờ địa phương Việt Nam).</summary>
    DateTime CreationTime { get; set; }
}

// Xóa mềm (soft delete): thay vì DELETE khỏi bảng, bản ghi chỉ được đánh dấu IsDeleted = true.
// Lý do: đơn hàng cũ, giỏ hàng... vẫn tham chiếu khóa ngoại tới sản phẩm/biến thể/người dùng; xóa cứng sẽ làm
// mất lịch sử hoặc vi phạm ràng buộc khóa ngoại. Áp dụng cho UC Quản lý sản phẩm và biến thể (Bảng 3.10 –
// xóa mềm) và UC Quản lý người dùng (Bảng 3.13).
// Cơ chế (ở tầng Infrastructure): HornVietDbContext đăng ký bộ lọc toàn cục x => !x.IsDeleted cho mọi lớp cài
// interface này, và đổi trạng thái Deleted thành Modified + IsDeleted = true khi SaveChanges.
/// <summary>Thực thể xóa mềm: lệnh xóa được DbContext đổi thành IsDeleted = true và bị lọc khỏi mọi truy vấn.</summary>
public interface ISoftDelete
{
    /// <summary>true = bản ghi đã bị xóa mềm, không còn hiển thị trong các truy vấn thông thường.</summary>
    bool IsDeleted { get; set; }
}

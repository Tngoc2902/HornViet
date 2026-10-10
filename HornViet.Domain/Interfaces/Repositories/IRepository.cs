using HornViet.Domain.SharedKernel;

namespace HornViet.Domain.Interfaces.Repositories;

/// <summary>
/// Repository (Kho lưu trữ) generic theo DDD: lớp trừu tượng truy cập dữ liệu cho một loại thực thể.
/// Interface đặt ở tầng Domain, còn lớp cài đặt Repository&lt;TEntity, TKey&gt; nằm ở tầng Infrastructure (dùng
/// EF Core). Nhờ đảo ngược phụ thuộc (Dependency Inversion), tầng Application chỉ làm việc với interface này
/// qua Dependency Injection, không phụ thuộc trực tiếp vào EF Core hay SQL Server.
/// </summary>
/// <typeparam name="TEntity">Kiểu thực thể, VD Product, Order.</typeparam>
/// <typeparam name="TKey">Kiểu khóa chính của thực thể, VD int, long.</typeparam>
// Generic với hai tham số kiểu: một interface dùng chung cho mọi thực thể, VD IRepository<Order, int>,
// IRepository<User, long> — không phải viết riêng repository cho từng bảng.
// "where TEntity : Entity<TKey>": ràng buộc generic — chỉ chấp nhận kiểu kế thừa Entity<TKey>, đảm bảo
// thực thể luôn có thuộc tính Id cùng kiểu với TKey.
public interface IRepository<TEntity, TKey> where TEntity : Entity<TKey>
{
    // Trả về IQueryable (truy vấn chưa thực thi) thay vì List: tầng Application có thể nối thêm
    // Where/OrderBy/Include/Skip/Take; EF Core dịch toàn bộ thành một câu SQL và chỉ chạy khi gọi
    // ToListAsync/FirstOrDefaultAsync... Nhờ vậy lọc, phân trang được thực hiện ngay trong CSDL.
    /// <summary>Truy vấn gốc (đã áp dụng bộ lọc xóa mềm).</summary>
    IQueryable<TEntity> GetAll();

    /// <summary>
    /// Đánh dấu thực thể mới cần thêm vào CSDL. Chưa ghi xuống ngay: bản ghi chỉ thực sự được INSERT khi
    /// gọi IUnitOfWork.SaveChangesAsync.
    /// </summary>
    /// <param name="entity">Thực thể cần thêm.</param>
    /// <param name="ct">Token hủy thao tác (khi client ngắt kết nối); "= default" nghĩa là tham số tùy chọn.</param>
    /// <returns>Task — phương thức bất đồng bộ (async), dùng với await.</returns>
    Task InsertAsync(TEntity entity, CancellationToken ct = default);

    // Lớp cài đặt ở Infrastructure: nếu thực thể cài ISoftDelete thì gán thẳng IsDeleted = true (xóa mềm,
    // tránh EF xóa dây chuyền các thực thể con đang được theo dõi); ngược lại mới xóa thật khỏi bảng.
    // Giống InsertAsync, thay đổi chỉ được ghi xuống CSDL khi gọi SaveChangesAsync.
    /// <summary>Xóa; với ISoftDelete sẽ thành xóa mềm khi SaveChanges.</summary>
    void Delete(TEntity entity);
}

/// <summary>
/// Đơn vị công việc: mọi thay đổi trong một use case được ghi xuống CSDL bằng một lần SaveChanges
/// (một giao dịch duy nhất) — tạo đơn, trừ kho, xóa giỏ hoặc cùng thành công hoặc cùng hoàn tác.
/// </summary>
// Unit of Work (Đơn vị công việc): các repository chỉ ghi nhận thay đổi trong bộ nhớ (Change Tracker của EF Core);
// IUnitOfWork mới là nơi "chốt" toàn bộ thay đổi xuống CSDL. Lớp cài đặt chính là HornVietDbContext ở tầng
// Infrastructure. Mỗi request HTTP dùng chung một DbContext (đăng ký Scoped) nên mọi repository trong cùng
// use case chia sẻ cùng một Unit of Work.
// VD UC Đặt hàng (Bảng 3.8): thêm Order, trừ StockQuantity của từng ProductVariant, xóa các CartItem —
// tất cả nằm trong một lần SaveChangesAsync, nếu một bước lỗi thì không bước nào được ghi.
/// <summary>
/// Đơn vị công việc: mọi thay đổi trong một use case được ghi xuống CSDL bằng một lần SaveChanges
/// (một giao dịch duy nhất) — tạo đơn, trừ kho, xóa giỏ hoặc cùng thành công hoặc cùng hoàn tác.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Ghi mọi thay đổi đang được theo dõi xuống CSDL trong một giao dịch. Trước khi ghi, DbContext tự gán
    /// CreationTime cho bản ghi mới và đổi lệnh xóa thực thể ISoftDelete thành xóa mềm.
    /// </summary>
    /// <param name="ct">Token hủy thao tác, tùy chọn.</param>
    /// <remarks>
    /// Có thể ném DbUpdateConcurrencyException (của EF Core) khi RowVersion của biến thể đã bị giao dịch khác
    /// thay đổi (khóa lạc quan) — middleware ResponseHandling đổi lỗi này thành HTTP 409.
    /// </remarks>
    Task SaveChangesAsync(CancellationToken ct = default);
}

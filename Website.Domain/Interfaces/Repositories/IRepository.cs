using Website.Domain.SharedKernel;

namespace Website.Domain.Interfaces.Repositories;

public interface IRepository<TEntity, TKey> where TEntity : Entity<TKey>
{
    /// <summary>Truy vấn gốc (đã áp dụng bộ lọc xóa mềm).</summary>
    IQueryable<TEntity> GetAll();

    Task InsertAsync(TEntity entity, CancellationToken ct = default);

    /// <summary>Xóa; với ISoftDelete sẽ thành xóa mềm khi SaveChanges.</summary>
    void Delete(TEntity entity);
}

/// <summary>
/// Đơn vị công việc: mọi thay đổi trong một use case được ghi xuống CSDL bằng một lần SaveChanges
/// (một giao dịch duy nhất) — tạo đơn, trừ kho, xóa giỏ hoặc cùng thành công hoặc cùng hoàn tác.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}

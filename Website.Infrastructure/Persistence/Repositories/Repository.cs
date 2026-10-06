using Website.Domain.Interfaces.Repositories;
using Website.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Website.Infrastructure.Persistence.Repositories;

public class Repository<TEntity, TKey> : IRepository<TEntity, TKey> where TEntity : Entity<TKey>
{
    private readonly WebsiteDbContext _db;

    public Repository(WebsiteDbContext db) => _db = db;

    private DbSet<TEntity> Set => _db.Set<TEntity>();

    public IQueryable<TEntity> GetAll() => Set;

    public async Task InsertAsync(TEntity entity, CancellationToken ct = default) => await Set.AddAsync(entity, ct);

    public void Delete(TEntity entity)
    {
        // Xóa mềm trực tiếp để EF không xóa dây chuyền các thực thể con đang được theo dõi.
        if (entity is ISoftDelete softDelete)
            softDelete.IsDeleted = true;
        else
            Set.Remove(entity);
    }
}

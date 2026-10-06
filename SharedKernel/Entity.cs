namespace Website.Domain.SharedKernel;

/// <summary>Lớp cơ sở cho mọi thực thể có khóa chính kiểu <typeparamref name="TKey"/>.</summary>
public abstract class Entity<TKey>
{
    public TKey Id { get; set; } = default!;
}

/// <summary>Thời điểm tạo được DbContext gán tự động khi thêm mới.</summary>
public interface IHasCreationTime
{
    DateTime CreationTime { get; set; }
}

/// <summary>Thực thể xóa mềm: lệnh xóa được DbContext đổi thành IsDeleted = true và bị lọc khỏi mọi truy vấn.</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}

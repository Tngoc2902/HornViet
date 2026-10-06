namespace Website.Domain.SharedKernel;

/// <summary>
/// Lỗi nghiệp vụ có thông điệp hiển thị được cho người dùng (tương đương UserFriendlyException của ABP).
/// Middleware trả về HTTP 400 kèm thông điệp này.
/// </summary>
public class UserFriendlyException : Exception
{
    public string? Details { get; }

    public UserFriendlyException(string message, string? details = null) : base(message)
    {
        Details = details;
    }
}

/// <summary>Không tìm thấy tài nguyên — middleware trả về HTTP 404.</summary>
public class EntityNotFoundException : UserFriendlyException
{
    public EntityNotFoundException(string message) : base(message) { }
}

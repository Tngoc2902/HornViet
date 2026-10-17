using HornViet.Domain.SharedKernel;

namespace HornViet.Domain.Users.Entities;

/// <summary>Tài khoản người dùng (bảng AbpUsers).</summary>
public class User : Entity<long>, IHasCreationTime, ISoftDelete
{
    public string UserName { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string EmailAddress { get; set; } = null!;
    public string Password { get; set; } = null!; // chỉ lưu hash; rỗng = tài khoản tạo bằng Google, chưa đặt mật khẩu
    public string? PhoneNumber { get; set; }

    /// <summary>Mã định danh cố định của tài khoản Google ("sub") đã liên kết; null = chưa liên kết.</summary>
    public string? GoogleId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreationTime { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Dấu bảo mật, nhúng vào JWT khi đăng nhập. Mỗi request đối chiếu dấu trong token với giá trị này;
    /// đổi / đặt lại mật khẩu hoặc đổi vai trò sẽ tạo dấu mới nên mọi token phát hành trước đó mất hiệu lực ngay.
    /// </summary>
    public string SecurityStamp { get; private set; } = NewSecurityStamp();

    public List<UserRole> Roles { get; set; } = new();
    public List<UserAddress> Addresses { get; set; } = new();

    /// <summary>Đã có mật khẩu để đăng nhập bằng email / tên đăng nhập (tài khoản tạo bằng Google thì chưa).</summary>
    public bool HasPassword => !string.IsNullOrEmpty(Password);

    /// <summary>Thu hồi mọi phiên đăng nhập hiện có của tài khoản (token cũ không còn hợp lệ).</summary>
    public void RotateSecurityStamp() => SecurityStamp = NewSecurityStamp();

    private static string NewSecurityStamp() => Guid.NewGuid().ToString("N");
}

/// <summary>Vai trò (bảng AbpRoles).</summary>
public class Role : Entity<int>, IHasCreationTime, ISoftDelete
{
    public string Name { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public DateTime CreationTime { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>Quan hệ nhiều – nhiều người dùng – vai trò (bảng AbpUserRoles).</summary>
public class UserRole : Entity<long>, IHasCreationTime
{
    public long UserId { get; set; }
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public DateTime CreationTime { get; set; }
}

/// <summary>Sổ địa chỉ giao hàng của khách hàng.</summary>
public class UserAddress : Entity<int>, IHasCreationTime
{
    public long UserId { get; set; }
    public string ReceiverName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string Address { get; set; } = null!;
    public bool IsDefault { get; set; }
    public DateTime CreationTime { get; set; }
}


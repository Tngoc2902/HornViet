using Website.Domain.SharedKernel;

namespace Website.Domain.Users.Entities;

/// <summary>Tài khoản người dùng (bảng AbpUsers).</summary>
public class User : Entity<long>, IHasCreationTime, ISoftDelete
{
    public string UserName { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string EmailAddress { get; set; } = null!;
    public string Password { get; set; } = null!; // chỉ lưu hash
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreationTime { get; set; }
    public bool IsDeleted { get; set; }

    public List<UserRole> Roles { get; set; } = new();
    public List<UserAddress> Addresses { get; set; } = new();
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

public static class StaticRoleNames
{
    public const string Admin = "Admin";       // Quản trị viên
    public const string Staff = "Staff";       // Nhân viên
    public const string Customer = "Customer"; // Khách hàng
}

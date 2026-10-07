using Website.Domain.Catalog.Entities;
using Website.Domain.Interfaces.Repositories;
using Website.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Website.Domain.Users.Enums;

namespace Website.Application;

public class SeedOptions
{
    public string AdminPassword { get; set; } = "123qwe";
    public string StaffPassword { get; set; } = "123qwe";
    /// <summary>Tạo danh mục, nguyên liệu, sản phẩm mẫu khi CSDL còn trống.</summary>
    public bool SampleCatalog { get; set; } = true;
}

/// <summary>
/// Khởi tạo dữ liệu ban đầu (vai trò, tài khoản quản trị/nhân viên demo, danh mục mẫu).
/// Chạy lặp lại an toàn: chỉ thêm phần còn thiếu. Không tạo dữ liệu khách hàng.
/// </summary>
public class DataSeeder
{
    private readonly IRepository<Role, int> _roles;
    private readonly IRepository<User, long> _users;
    private readonly IRepository<Category, int> _categories;
    private readonly IRepository<Material, int> _materials;
    private readonly IRepository<Product, int> _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;

    public DataSeeder(IRepository<Role, int> roles, IRepository<User, long> users, IRepository<Category, int> categories,
        IRepository<Material, int> materials, IRepository<Product, int> products, IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher)
    {
        _roles = roles;
        _users = users;
        _categories = categories;
        _materials = materials;
        _products = products;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task SeedAsync(SeedOptions options)
    {
        await SeedRolesAsync();
        await SeedUserAsync("admin", "Quản trị viên", "admin@honsungviet.local", options.AdminPassword, nameof(RoleName.Admin));
        await SeedUserAsync("staff", "Nhân viên", "staff@honsungviet.local", options.StaffPassword, nameof(RoleName.Staff));
        if (options.SampleCatalog)
            await SeedCatalogAsync();
    }

    private async Task SeedRolesAsync()
    {
        var wanted = new[]
        {
            (nameof(RoleName.Admin), "Quản trị viên"),
            (nameof(RoleName.Staff), "Nhân viên"),
            (nameof(RoleName.Customer), "Khách hàng")
        };
        var existing = await _roles.GetAll().Select(r => r.Name).ToListAsync();
        foreach (var (name, display) in wanted.Where(w => !existing.Contains(w.Item1)))
            await _roles.InsertAsync(new Role { Name = name, DisplayName = display });
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task SeedUserAsync(string userName, string name, string email, string password, string roleName)
    {
        if (await _users.GetAll().AnyAsync(u => u.UserName == userName))
            return;

        var role = await _roles.GetAll().SingleAsync(r => r.Name == roleName);
        var user = new User { UserName = userName, Name = name, EmailAddress = email, IsActive = true };
        user.Password = _passwordHasher.HashPassword(user, password);
        user.Roles.Add(new UserRole { RoleId = role.Id });
        await _users.InsertAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task SeedCatalogAsync()
    {
        if (await _categories.GetAll().AnyAsync())
            return;

        var names = new[] { "Lược sừng", "Trâm cài", "Vòng tay, nhẫn", "Đồ gia dụng", "Đồ trang trí", "Móc khóa" };
        var categories = names.Select(n => new Category { Name = n }).ToList();
        foreach (var c in categories) await _categories.InsertAsync(c);

        var buffalo = new Material
        {
            Name = "Sừng trâu",
            Origin = "Làng nghề Thụy Ứng, Hà Nội",
            Description = "Sừng trâu tự nhiên thu từ trâu nuôi lấy thịt, xử lý và mài thủ công."
        };
        var ox = new Material
        {
            Name = "Sừng bò",
            Origin = "Thu mua trong nước",
            Description = "Sừng bò tự nhiên, vân sáng màu, đã qua xử lý khử mùi."
        };
        await _materials.InsertAsync(buffalo);
        await _materials.InsertAsync(ox);
        await _unitOfWork.SaveChangesAsync();

        Product Make(int categoryIndex, Material material, string name, decimal price, params (string? Size, string? Style, int Stock)[] variants)
        {
            var p = new Product
            {
                CategoryId = categories[categoryIndex].Id,
                MaterialId = material.Id,
                Name = name,
                Price = price,
                MainImage = "/uploads/sample/placeholder.svg",
                Description = $"{name} chế tác thủ công từ {material.Name.ToLower()}.",
                IsActive = true
            };
            foreach (var v in variants) p.Variants.Add(new ProductVariant(v.Size, v.Style, v.Stock));
            return p;
        }

        var products = new[]
        {
            Make(0, buffalo, "Lược sừng trâu chạm khắc", 180_000, ("Nhỏ", "Răng thưa", 12), ("Vừa", "Răng thưa", 8), ("Lớn", "Răng dày", 0)),
            Make(0, buffalo, "Lược sừng trâu răng thưa", 120_000, ("Vừa", null, 20)),
            Make(0, ox, "Lược sừng bò bỏ túi", 90_000, ("Mini", null, 3)),
            Make(1, buffalo, "Trâm cài sừng hoa sen", 150_000, (null, "Hoa sen", 6), (null, "Chim lạc", 4)),
            Make(2, buffalo, "Vòng tay sừng trơn", 250_000, ("Cỡ 56", "Trơn", 5), ("Cỡ 58", "Trơn", 7), ("Cỡ 60", "Trơn", 2)),
            Make(3, ox, "Thìa sừng ăn cơm", 45_000, (null, null, 40)),
            Make(5, buffalo, "Móc khóa sừng hình cá", 60_000, (null, "Cá chép", 15))
        };
        foreach (var p in products) await _products.InsertAsync(p);
        await _unitOfWork.SaveChangesAsync();
    }
}

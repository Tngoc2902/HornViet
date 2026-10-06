using System.Linq.Expressions;
using Website.Domain.Carts.Entities;
using Website.Domain.Catalog.Entities;
using Website.Domain.Interfaces.Repositories;
using Website.Domain.Orders.Entities;
using Website.Domain.SharedKernel;
using Website.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Website.Infrastructure.Persistence;

public class WebsiteDbContext : DbContext, IUnitOfWork
{
    public WebsiteDbContext(DbContextOptions<WebsiteDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Không phân biệt hoa thường và dấu (kể cả ư, ơ, đ): khách gõ "luoc sung" vẫn tìm ra "Lược sừng".
        // Vietnamese_CI_AI coi ư/ơ/đ là chữ riêng nên không đạt yêu cầu này.
        b.UseCollation("Latin1_General_100_CI_AI");

        // Mọi quan hệ khai báo hành vi xóa tường minh: Cascade chỉ cho quan hệ hợp thành
        // (Product–Variant/Image, Cart–Item, Order–Item, User–Role/Address), còn lại Restrict.
        b.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        b.Entity<Material>(e =>
        {
            e.ToTable("Materials");
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.Property(x => x.Origin).HasMaxLength(255);
            e.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        b.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.MainImage).HasMaxLength(500).IsRequired();
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Variants).WithOne(v => v.Product).HasForeignKey(v => v.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Images).WithOne().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.IsActive, x.CategoryId });
        });

        b.Entity<ProductVariant>(e =>
        {
            e.ToTable("ProductVariants");
            e.Property(x => x.Size).HasMaxLength(100);
            e.Property(x => x.Style).HasMaxLength(100);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.DisplayName);
        });

        b.Entity<ProductImage>(e =>
        {
            e.ToTable("ProductImages");
            e.Property(x => x.ImageUrl).HasMaxLength(500).IsRequired();
        });

        b.Entity<Cart>(e =>
        {
            e.ToTable("Carts");
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.CartId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CartItem>(e =>
        {
            e.ToTable("CartItems");
            e.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CartId, x.ProductVariantId }).IsUnique();
        });

        b.Entity<Order>(e =>
        {
            e.ToTable("Orders");
            e.Property(x => x.ReceiverName).HasMaxLength(255).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(x => x.ShippingAddress).HasMaxLength(500).IsRequired();
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.CreationTime });
            e.HasIndex(x => new { x.Status, x.CreationTime });
        });

        b.Entity<OrderItem>(e =>
        {
            e.ToTable("OrderItems");
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.ProductName).HasMaxLength(255).IsRequired();
            e.Property(x => x.VariantName).HasMaxLength(255).IsRequired();
            e.HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        });

        // Tên bảng người dùng / vai trò giữ theo quy ước ABP như trong thiết kế CSDL (mục 3.6).
        b.Entity<User>(e =>
        {
            e.ToTable("AbpUsers");
            e.Property(x => x.UserName).HasMaxLength(256).IsRequired();
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
            e.Property(x => x.EmailAddress).HasMaxLength(256).IsRequired();
            e.Property(x => x.Password).HasMaxLength(128).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(32);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.HasIndex(x => x.UserName).IsUnique().HasFilter("[IsDeleted] = 0");
            e.HasIndex(x => x.EmailAddress).IsUnique().HasFilter("[IsDeleted] = 0");
            e.HasMany(x => x.Roles).WithOne().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Addresses).WithOne().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Role>(e =>
        {
            e.ToTable("AbpRoles");
            e.Property(x => x.Name).HasMaxLength(32).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        b.Entity<UserRole>(e =>
        {
            e.ToTable("AbpUserRoles");
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();
        });

        b.Entity<UserAddress>(e =>
        {
            e.ToTable("UserAddresses");
            e.Property(x => x.ReceiverName).HasMaxLength(255).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(x => x.Address).HasMaxLength(500).IsRequired();
        });

        // Hệ thống lưu giờ địa phương (giờ Việt Nam); đánh dấu Kind = Local khi đọc ra để JSON luôn có offset +07:00.
        var localTime = new ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Local));
        var localTimeNullable = new ValueConverter<DateTime?, DateTime?>(
            v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Local) : v);
        foreach (var property in b.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (property.ClrType == typeof(DateTime)) property.SetValueConverter(localTime);
            else if (property.ClrType == typeof(DateTime?)) property.SetValueConverter(localTimeNullable);
        }

        // Bộ lọc xóa mềm toàn cục: x => !x.IsDeleted
        foreach (var entityType in b.Model.GetEntityTypes().Where(t => typeof(ISoftDelete).IsAssignableFrom(t.ClrType)))
        {
            var param = Expression.Parameter(entityType.ClrType, "x");
            var body = Expression.Not(Expression.Property(param, nameof(ISoftDelete.IsDeleted)));
            b.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, param));
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditConcepts();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditConcepts();
        return base.SaveChanges();
    }

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken ct) => await SaveChangesAsync(ct);

    private void ApplyAuditConcepts()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Entity is IHasCreationTime created && created.CreationTime == default)
                created.CreationTime = DateTime.Now;

            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete softDelete)
            {
                entry.State = EntityState.Modified;
                softDelete.IsDeleted = true;
            }
        }
    }
}

using System.Linq.Expressions;
using System.Reflection;
using HornViet.Application.Common;
using HornViet.Domain.Carts.Entities;
using HornViet.Domain.Catalog.Entities;
using HornViet.Domain.Chat.Entities;
using HornViet.Domain.Interfaces.Repositories;
using HornViet.Domain.Inventory.Entities;
using HornViet.Domain.Orders.Entities;
using HornViet.Domain.SharedKernel;
using HornViet.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HornViet.Infrastructure.Persistence;

public class HornVietDbContext : DbContext, IUnitOfWork
{
    public HornVietDbContext(DbContextOptions<HornVietDbContext> options) : base(options) { }

    /// <summary>Cho lớp con theo hệ quản trị CSDL khác (PostgreSqlHornVietDbContext).</summary>
    protected HornVietDbContext(DbContextOptions options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionProduct> CollectionProducts => Set<CollectionProduct>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();
    public DbSet<ChatIntent> ChatIntents => Set<ChatIntent>();
    public DbSet<ChatPhrase> ChatPhrases => Set<ChatPhrase>();
    public DbSet<ChatModel> ChatModels => Set<ChatModel>();
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        ConfigureDatabase(b);

        // Mọi quan hệ khai báo hành vi xóa tường minh: Cascade chỉ cho quan hệ hợp thành
        // (Product–Variant/Image, Cart–Item, Order–Item, User–Role/Address), còn lại Restrict.
        b.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.HasIndex(x => x.Name).IsUnique().HasFilter(NotDeletedFilter);
        });

        b.Entity<Material>(e =>
        {
            e.ToTable("Materials");
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.Property(x => x.Origin).HasMaxLength(255);
            e.Property(x => x.Image).HasMaxLength(500);
            e.HasIndex(x => x.Name).IsUnique().HasFilter(NotDeletedFilter);
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
            e.Property(x => x.ImageUrl).HasMaxLength(500);
            ConfigureConcurrency(e);
            // Giá vốn bình quân giữ 4 chữ số thập phân để phép chia bình quân không làm lệch tổng giá vốn.
            e.Property(x => x.AverageCost).HasPrecision(18, 4);
            e.Ignore(x => x.DisplayName);
        });

        b.Entity<ProductImage>(e =>
        {
            e.ToTable("ProductImages");
            e.Property(x => x.ImageUrl).HasMaxLength(500).IsRequired();
        });

        // Bộ sưu tập: liên kết sản phẩm thuộc về bộ sưu tập (Cascade); sản phẩm chỉ xóa mềm nên phía sản phẩm để Restrict.
        b.Entity<Collection>(e =>
        {
            e.ToTable("Collections");
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(120).IsUnicode(false).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.CoverImage).HasMaxLength(500);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.HasIndex(x => x.Slug).IsUnique().HasFilter(NotDeletedFilter);
            e.HasIndex(x => new { x.IsActive, x.DisplayOrder });
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.CollectionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CollectionProduct>(e =>
        {
            e.ToTable("CollectionProducts");
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CollectionId, x.ProductId }).IsUnique();
            // Sản phẩm đã xóa mềm không còn tính là thuộc bộ sưu tập.
            e.HasQueryFilter(x => !x.Product.IsDeleted);
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
            e.Property(x => x.ShippingFee).HasPrecision(18, 2);
            e.Property(x => x.ToWardCode).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.ToStreet).HasMaxLength(300);
            e.Property(x => x.GhnOrderCode).HasMaxLength(32).IsUnicode(false);
            e.Property(x => x.GhnStatus).HasMaxLength(50).IsUnicode(false);
            e.Ignore(x => x.PayableAmount);
            e.HasIndex(x => x.GhnOrderCode); // webhook tìm đơn theo mã vận đơn
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
            e.Property(x => x.UnitCost).HasPrecision(18, 4);
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
            e.Property(x => x.GoogleId).HasMaxLength(255).IsUnicode(false);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            // Mặc định ở CSDL: tài khoản đã có (khi thêm cột) và bản ghi chèn bằng script SQL cũng có dấu bảo mật.
            e.Property(x => x.SecurityStamp).HasMaxLength(32).IsUnicode(false).IsRequired()
                .HasDefaultValueSql(NewSecurityStampSql);
            e.HasIndex(x => x.UserName).IsUnique().HasFilter(NotDeletedFilter);
            e.HasIndex(x => x.EmailAddress).IsUnique().HasFilter(NotDeletedFilter);
            // Mỗi tài khoản Google chỉ liên kết với một người dùng.
            e.HasIndex(x => x.GoogleId).IsUnique().HasFilter($"{Quote(nameof(User.GoogleId))} IS NOT NULL AND {NotDeletedFilter}");
            e.HasMany(x => x.Roles).WithOne().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Addresses).WithOne().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Role>(e =>
        {
            e.ToTable("AbpRoles");
            e.Property(x => x.Name).HasMaxLength(32).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Name).IsUnique().HasFilter(NotDeletedFilter);
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
            e.Property(x => x.WardCode).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.Street).HasMaxLength(300);
        });

        // Phiếu nhập kho (chỉ Quản trị viên). Restrict: không xóa được biến thể / người dùng còn phiếu nhập tham chiếu.
        b.Entity<StockReceipt>(e =>
        {
            e.ToTable("StockReceipts");
            e.Property(x => x.UnitCost).HasPrecision(18, 2);
            e.Property(x => x.AverageCostBefore).HasPrecision(18, 4);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatorUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ProductVariantId, x.Id });
            e.HasIndex(x => x.CreationTime);
        });

        // UC Quên mật khẩu: chỉ lưu SHA-256 của mã (hex 64 ký tự). Mã thuộc về tài khoản nên xóa dây chuyền theo User.
        b.Entity<PasswordResetToken>(e =>
        {
            e.ToTable("PasswordResetTokens");
            e.Property(x => x.TokenHash).HasMaxLength(64).IsUnicode(false).IsFixedLength().IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.UserId, x.CreationTime });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Chatbot: ý định + câu mẫu (dữ liệu huấn luyện), model đã huấn luyện, hội thoại hỗ trợ và tin nhắn.
        b.Entity<ChatIntent>(e =>
        {
            e.ToTable("ChatIntents");
            e.Property(x => x.Code).HasMaxLength(64).IsUnicode(false).IsRequired();
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.Answer).HasMaxLength(2000).IsRequired();
            e.Property(x => x.LinkLabel).HasMaxLength(64);
            e.Property(x => x.LinkUrl).HasMaxLength(300).IsUnicode(false);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasMany(x => x.Phrases).WithOne().HasForeignKey(p => p.IntentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ChatPhrase>(e =>
        {
            e.ToTable("ChatPhrases");
            e.Property(x => x.Text).HasMaxLength(300).IsRequired();
        });

        b.Entity<ChatModel>(e =>
        {
            e.ToTable("ChatModels");
            e.Property(x => x.Data).IsRequired();
            e.Property(x => x.DataHash).HasMaxLength(64).IsUnicode(false).IsFixedLength().IsRequired();
            e.Property(x => x.Report).IsRequired();
        });

        b.Entity<ChatConversation>(e =>
        {
            e.ToTable("ChatConversations");
            e.Property(x => x.GuestKey).HasMaxLength(64).IsUnicode(false);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.StaffUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Messages).WithOne().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.Status });
            e.HasIndex(x => new { x.Status, x.LastMessageTime });
        });

        b.Entity<ChatMessage>(e =>
        {
            e.ToTable("ChatMessages");
            e.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            e.Property(x => x.IntentCode).HasMaxLength(64).IsUnicode(false);
            e.HasIndex(x => new { x.ConversationId, x.Id });
            e.HasIndex(x => new { x.NeedsReview, x.Id }); // danh sách "Câu bot chưa hiểu"
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

    // ---- Phần phụ thuộc hệ quản trị CSDL. Mặc định cho SQL Server (máy phát triển);
    //      PostgreSqlHornVietDbContext ghi đè cho PostgreSQL (triển khai cloud).

    protected static readonly MethodInfo SearchTextFold = typeof(SearchText).GetMethod(nameof(SearchText.Fold))!;

    /// <summary>Tên cột trong câu SQL viết tay (bộ lọc index): SQL Server dùng [Cột].</summary>
    protected virtual string Quote(string column) => $"[{column}]";

    /// <summary>Điều kiện "chưa xóa mềm" của index lọc — unique chỉ tính trên bản ghi chưa xóa.</summary>
    protected virtual string NotDeletedFilter => $"{Quote(nameof(ISoftDelete.IsDeleted))} = 0";

    /// <summary>Giá trị mặc định ở CSDL cho SecurityStamp: 32 ký tự hex ngẫu nhiên.</summary>
    protected virtual string NewSecurityStampSql => "REPLACE(CONVERT(char(36), NEWID()), '-', '')";

    /// <summary>Thiết lập chung của CSDL: so sánh chuỗi và hàm tìm kiếm SearchText.Fold.</summary>
    protected virtual void ConfigureDatabase(ModelBuilder b)
    {
        // Không phân biệt hoa thường và dấu (kể cả ư, ơ, đ): khách gõ "luoc sung" vẫn tìm ra "Lược sừng".
        // Vietnamese_CI_AI coi ư/ơ/đ là chữ riêng nên không đạt yêu cầu này.
        b.UseCollation("Latin1_General_100_CI_AI");
        // Collation trên đã không phân biệt hoa thường và dấu → SearchText.Fold giữ nguyên giá trị.
        b.HasDbFunction(SearchTextFold).HasTranslation(args => args[0]);
    }

    /// <summary>Khóa lạc quan cho tồn kho của biến thể: SQL Server dùng cột rowversion tự tăng.</summary>
    protected virtual void ConfigureConcurrency(EntityTypeBuilder<ProductVariant> e) =>
        e.Property(x => x.RowVersion).IsRowVersion();

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

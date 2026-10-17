using HornViet.Domain.Catalog.Entities;
using HornViet.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace HornViet.Infrastructure.Persistence.PostgreSql;

/// <summary>
/// HornVietDbContext cho PostgreSQL — dùng khi triển khai trên cloud (Neon), chọn bằng Database:Provider = PostgreSql.
/// Cùng mô hình dữ liệu với SQL Server, chỉ ghi đè phần phụ thuộc hệ quản trị CSDL; migration riêng nằm ở
/// Persistence/PostgreSql/Migrations. Repository, UnitOfWork và các Application Service không phải đổi gì.
/// </summary>
public class PostgreSqlHornVietDbContext : HornVietDbContext
{
    public PostgreSqlHornVietDbContext(DbContextOptions<PostgreSqlHornVietDbContext> options) : base(options) { }

    protected override string Quote(string column) => $"\"{column}\"";

    protected override string NotDeletedFilter => $"{Quote(nameof(ISoftDelete.IsDeleted))} = false";

    protected override string NewSecurityStampSql => "replace(gen_random_uuid()::text, '-', '')";

    protected override void ConfigureDatabase(ModelBuilder b)
    {
        // So sánh chuỗi của PostgreSQL phân biệt hoa thường và dấu → tìm kiếm qua SearchText.Fold = lower(unaccent(...)).
        // unaccent bỏ dấu tiếng Việt (ư→u, ơ→o, đ→d...) trước, nên lower() chỉ còn chữ ASCII — không phụ thuộc collation.
        b.HasPostgresExtension("unaccent");
        b.HasDbFunction(SearchTextFold).HasTranslation(args =>
        {
            var value = args[0];
            var unaccent = new SqlFunctionExpression("unaccent", new[] { value }, nullable: true,
                argumentsPropagateNullability: new[] { true }, typeof(string), value.TypeMapping);
            return new SqlFunctionExpression("lower", new[] { unaccent }, nullable: true,
                argumentsPropagateNullability: new[] { true }, typeof(string), value.TypeMapping);
        });
    }

    // PostgreSQL không có kiểu rowversion: dùng cột hệ thống xmin (tự đổi mỗi khi dòng được cập nhật) làm khóa lạc quan.
    // Hai đơn hàng cùng trừ tồn kho một biến thể → đơn sau nhận DbUpdateConcurrencyException (HTTP 409) như trên SQL Server.
    protected override void ConfigureConcurrency(EntityTypeBuilder<ProductVariant> e)
    {
        e.Ignore(x => x.RowVersion);
        e.Property<uint>("xmin").IsRowVersion();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        base.ConfigureConventions(c);
        // Hệ thống lưu giờ địa phương (DateTime.Now) → cột timestamp không kèm múi giờ, giống datetime2 của SQL Server.
        c.Properties<DateTime>().HaveColumnType("timestamp without time zone");
        c.Properties<DateTime?>().HaveColumnType("timestamp without time zone");
    }
}

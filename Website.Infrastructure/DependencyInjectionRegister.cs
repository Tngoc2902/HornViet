using Website.Application.Common;
using Website.Domain.Interfaces.Repositories;
using Website.Infrastructure.Persistence;
using Website.Infrastructure.Persistence.Repositories;
using Website.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Website.Infrastructure;

public static class DependencyInjectionRegister
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Default.");

        return services
            .AddPersistence(connectionString)
            .AddEmail(configuration);
    }

    /// <summary>Gửi email qua SMTP (mục "Smtp"); Host trống thì lưu email thành file .eml để thử ở môi trường dev.</summary>
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        return services;
    }

    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<WebsiteDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql
                .EnableRetryOnFailure()
                // Tách truy vấn khi Include nhiều collection (biến thể + ảnh) để tránh nhân bản dòng.
                .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                // Có chủ đích: dòng giỏ/vai trò trỏ tới bản ghi đã xóa mềm bị lọc bỏ cùng — app service đã xử lý.
                .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<WebsiteDbContext>());
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        return services;
    }
}

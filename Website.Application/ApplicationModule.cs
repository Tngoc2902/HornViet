using Website.Application.Accounts;
using Website.Application.Carts;
using Website.Application.Catalog;
using Website.Application.Orders;
using Website.Application.Profiles;
using Website.Application.Statistics;
using Website.Application.Users;
using Website.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Website.Application;

public static class ApplicationModule
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddScoped<IAccountAppService, AccountAppService>();
        services.AddScoped<IProfileAppService, ProfileAppService>();
        services.AddScoped<ICategoryAppService, CategoryAppService>();
        services.AddScoped<IMaterialAppService, MaterialAppService>();
        services.AddScoped<IProductAppService, ProductAppService>();
        services.AddScoped<ICartAppService, CartAppService>();
        services.AddScoped<IOrderAppService, OrderAppService>();
        services.AddScoped<IUserAppService, UserAppService>();
        services.AddScoped<IStatisticAppService, StatisticAppService>();
        services.AddScoped<DataSeeder>();
        return services;
    }
}

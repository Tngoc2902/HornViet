using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Website.Api.Middlewares;
using Website.Api.Models;
using Website.Application.Accounts;
using Website.Application.Common;
using Website.Domain.Interfaces.Repositories;
using Website.Domain.Users.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Website.Api.Authorization;

public class JwtOptions
{
    public string Issuer { get; set; } = "HonSungViet";
    public string Audience { get; set; } = "HonSungViet";
    /// <summary>Khóa ký HMAC — tối thiểu 32 ký tự; môi trường thật đặt qua biến môi trường / user-secrets.</summary>
    public string SecurityKey { get; set; } = null!;
    public int ExpirationHours { get; set; } = 24;
}

public static class AuthorizationPolicies
{
    /// <summary>Nhân viên hoặc Quản trị viên.</summary>
    public const string Management = "Management";

    /// <summary>Chỉ Quản trị viên.</summary>
    public const string AdminOnly = "AdminOnly";
}

public class TokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public (string Token, int ExpireInSeconds) CreateToken(AuthenticatedUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var expires = TimeSpan.FromHours(_options.ExpirationHours);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expires),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecurityKey)), SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), (int)expires.TotalSeconds);
    }
}

/// <summary>IAppSession lấy từ JWT của request hiện tại.</summary>
public class HttpContextAppSession : IAppSession
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextAppSession(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public long? UserId =>
        long.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public bool IsInRole(string roleName) => Principal?.IsInRole(roleName) ?? false;
}

public static class AuthenticationSetup
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("Jwt");
        services.Configure<JwtOptions>(section);
        var jwt = section.Get<JwtOptions>() ?? throw new InvalidOperationException("Thiếu cấu hình Jwt.");
        if (string.IsNullOrEmpty(jwt.SecurityKey) || jwt.SecurityKey.Length < 32)
            throw new InvalidOperationException("Jwt:SecurityKey phải có tối thiểu 32 ký tự.");

        services.AddSingleton<TokenService>();
        services.AddHttpContextAccessor();
        services.AddScoped<IAppSession, HttpContextAppSession>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecurityKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    // Tài khoản bị khóa / xóa thì token đã cấp mất hiệu lực ngay.
                    OnTokenValidated = async ctx =>
                    {
                        var userIdValue = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        var users = ctx.HttpContext.RequestServices.GetRequiredService<IRepository<User, long>>();
                        var active = long.TryParse(userIdValue, out var userId) &&
                                     await users.GetAll().AnyAsync(u => u.Id == userId && u.IsActive);
                        if (!active)
                            ctx.Fail("Tài khoản không còn hoạt động.");
                    },
                    OnChallenge = async ctx =>
                    {
                        ctx.HandleResponse();
                        await ExceptionHandlingMiddleware.WriteAsync(ctx.HttpContext, StatusCodes.Status401Unauthorized,
                            AjaxResponse.Fail("Bạn chưa đăng nhập hoặc phiên đăng nhập đã hết hạn.", unauthorized: true));
                    },
                    OnForbidden = ctx => ExceptionHandlingMiddleware.WriteAsync(ctx.HttpContext, StatusCodes.Status403Forbidden,
                        AjaxResponse.Fail("Bạn không có quyền thực hiện chức năng này."))
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.Management, p => p.RequireRole(StaticRoleNames.Admin, StaticRoleNames.Staff))
            .AddPolicy(AuthorizationPolicies.AdminOnly, p => p.RequireRole(StaticRoleNames.Admin));

        return services;
    }
}

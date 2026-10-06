using Website.Api.Authorization;
using Website.Api.Middlewares;
using Website.Application;
using Website.Infrastructure;
using Website.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddInfrastructure(config);
builder.Services.AddApplication();
builder.Services.AddJwtAuthentication(config);

builder.Services
    .AddControllers(o => o.Filters.Add<WrapResultFilter>())
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ValidationResponse.Create);

var corsOrigins = (config["App:CorsOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Hồn Sừng Việt API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Dán accessToken nhận từ /api/TokenAuth/Authenticate",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
    c.CustomSchemaIds(t => t.FullName?.Replace("+", "."));
});

var app = builder.Build();

if (config.GetValue("Database:MigrateOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<WebsiteDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DataSeeder>()
        .SeedAsync(config.GetSection("Seed").Get<SeedOptions>() ?? new SeedOptions());
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.DocumentTitle = "Hồn Sừng Việt API");
}

app.UseStaticFiles(); // wwwroot/uploads — ảnh sản phẩm
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;

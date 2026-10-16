using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Application.Customers;
using OpticaSosa.Infrastructure.Customers;
using OpticaSosa.Application.Business;
using OpticaSosa.Infrastructure.Business;

namespace OpticaSosa.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, StoragePaths paths)
    {
        Directory.CreateDirectory(paths.DataDirectory);
        Directory.CreateDirectory(paths.AttachmentsDirectory);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabaseFile,
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = 5
        }.ToString();

        services.AddSingleton(paths);
        services.AddDbContextFactory<OpticaDbContext>(options => options.UseSqlite(connectionString));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<OpticaDbContext>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ISecurityAudit, SecurityAudit>();
        services.AddScoped<LocalProvisioning>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IVisitService, VisitService>();
        services.AddScoped<IBusinessSettingsService, BusinessSettingsService>();
        return services;
    }
}

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Viewer;
using OpticaSosa.Infrastructure.Backups;
using OpticaSosa.Infrastructure.Clinical;
using OpticaSosa.Infrastructure.Customers;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;
using OpticaSosa.Infrastructure.Viewer;
using OpticaSosa.Infrastructure.Work;

namespace OpticaSosa.Viewer;

public partial class ViewerEntryPoint
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var url = builder.Configuration["urls"] ?? "http://127.0.0.1:5092";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address) || address.Host != "127.0.0.1" || address.Scheme != "http")
            throw new InvalidOperationException("El visor sólo escucha en http://127.0.0.1:PUERTO.");
        builder.WebHost.UseUrls(url);
        builder.WebHost.ConfigureKestrel(server =>
        {
            // Ignorar endpoints heredados de variables/appsettings: nunca abrir LAN.
            server.Configure(new ConfigurationBuilder().Build());
            server.Listen(System.Net.IPAddress.Loopback, address.Port);
            server.Limits.MaxRequestBodySize = 64 * 1024;
        });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        ConfigureServices(builder.Services, builder.Configuration);
        var app = builder.Build();
        app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("No se pudo completar la consulta. Conservá la copia y revisá el informe local; no se modificaron los datos.");
        }));
        app.Use(async (context, next) =>
        {
            if (context.Request.Host.Host is not ("127.0.0.1" or "localhost") ||
                context.Connection.RemoteIpAddress is { } ip && !System.Net.IPAddress.IsLoopback(ip))
            { context.Response.StatusCode = 403; return; }
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'none'; form-action 'self'";
            if (context.Request.Method is not ("GET" or "HEAD") &&
                !(HttpMethods.IsPost(context.Request.Method) && context.Request.Path.Value is "/cuenta/ingresar" or "/cuenta/salir" or "/accesos"))
            { context.Response.StatusCode = 405; return; }
            await next();
        });
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter(); app.UseAntiforgery();
        app.MapGet("/viewer.css", (IWebHostEnvironment environment) =>
            Results.File(Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "viewer.css"), "text/css"))
            .AllowAnonymous(); // Publicar únicamente este recurso; nunca un explorador estático genérico.
        app.MapRazorPages();
        await app.RunAsync();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var instance = Guid.NewGuid().ToString("N");
        services.AddSingleton(TimeProvider.System);
        services.AddAntiforgery(options => options.Cookie.Name = "OpticaViewer.Antiforgery." + instance);
        services.Configure<CookieTempDataProviderOptions>(options => options.Cookie.Name = "OpticaViewer.TempData." + instance);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddFixedWindowLimiter("viewer-login", limiter =>
            {
                limiter.PermitLimit = 20; limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0; limiter.AutoReplenishment = true;
            });
        });
        services.AddSingleton<ViewerSnapshot>();
        services.AddSingleton<ViewerJournal>();
        services.AddSingleton<IViewerAudit>(p => p.GetRequiredService<ViewerJournal>());
        services.AddSingleton<ISecurityAudit>(p => p.GetRequiredService<ViewerJournal>());
        services.AddSingleton<ViewerAccess>(); services.AddSingleton<ViewerReadGuard>();
        services.Configure<BackupRuntimeOptions>(configuration.GetSection("Backups"));
        services.PostConfigure<BackupRuntimeOptions>(o => { o.RequireSignature = true; o.SigningEnabled = false; o.WorkerEnabled = false; o.ExternalWritesAuthorized = false; });
        services.AddSingleton<IBackupSigningCertificate, WindowsBackupSigningCertificate>();
        services.AddSingleton<BackupAuthenticity>(); services.AddSingleton<IStorageCapacity, ViewerCapacity>();
        services.AddHostedService<ViewerPreparation>(); // Sin BackupWorker ni comandos de migración.
        services.AddDbContextFactory<OpticaDbContext>((provider, options) => options
            .UseSqlite(provider.GetRequiredService<ViewerSnapshot>().ConnectionString)
            .AddInterceptors(provider.GetRequiredService<ViewerReadGuard>()));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<OpticaDbContext>();
        services.AddLocalIdentitySecurity();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "OpticaViewer." + instance;
            var original = options.Events.OnValidatePrincipal;
            options.Events.OnValidatePrincipal = async context =>
            {
                await original(context);
                var id = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (id is not null && context.HttpContext.RequestServices.GetRequiredService<ViewerAccess>().IsRevoked(id))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            };
        });
        services.AddSingleton(p => new StoragePaths(p.GetRequiredService<ViewerSnapshot>().Directory,
            Path.Combine(Path.GetTempPath(), "OpticaViewer-public-unused")));
        services.AddOptions<ClinicalFileOptions>(); services.AddSingleton<ClinicalFileGate>();
        services.AddScoped<IPrivateAttachmentStore, ViewerAttachmentStore>();
        services.AddScoped<IClinicalFilePolicyService, ClinicalFilePolicyService>();
        services.AddScoped<ClinicalFileValidation>(); services.AddScoped<ClinicalAttachmentService>();
        services.AddScoped<CustomerService>(); services.AddScoped<ClinicalService>(); services.AddScoped<WorkService>();
        services.AddScoped<IViewerReader, ViewerReader>();
        services.AddScoped<IViewerAccounts, ViewerAccounts>();
        services.AddRazorPages();
    }
}

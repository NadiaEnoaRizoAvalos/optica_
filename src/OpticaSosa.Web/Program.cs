using OpticaSosa.Web.Components;
using OpticaSosa.Infrastructure;
using OpticaSosa.Infrastructure.Persistence;
using OpticaSosa.Web.Security;
using OpticaSosa.Application.Business;
using OpticaSosa.Application.Clinical;
using Microsoft.AspNetCore.Http.Features;

var hostArgs = args.Where(arg => arg is not ("--initialize-database" or "--create-admin" or "--recover-admin")).ToArray();
var builder = WebApplication.CreateBuilder(hostArgs);
builder.Services.AddWindowsService(options => options.ServiceName = "OpticaSosa");
builder.Services.Configure<OpticaSosa.Infrastructure.Backups.BackupRuntimeOptions>(builder.Configuration.GetSection("Backups"));
builder.Services.AddHostedService<OpticaSosa.Web.Backups.BackupWorker>();

var dataDirectory = builder.Configuration["Storage:DataDirectory"];
if (builder.Environment.IsDevelopment())
{
    dataDirectory = Path.GetFullPath(
        dataDirectory ?? "../../data", builder.Environment.ContentRootPath);
}
else if (string.IsNullOrWhiteSpace(dataDirectory))
{
    throw new InvalidOperationException("Configure Storage:DataDirectory con una ruta local absoluta y privada.");
}

builder.Services.AddPersistence(new StoragePaths(
    dataDirectory!, builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot")));
builder.Services.Configure<ClinicalFileOptions>(builder.Configuration.GetSection(ClinicalFileOptions.Section));
var fileOptions = builder.Configuration.GetSection(ClinicalFileOptions.Section).Get<ClinicalFileOptions>() ?? new();
if (!fileOptions.Valid()) throw new InvalidOperationException("Configuración técnica de archivos clínicos inválida.");
builder.WebHost.ConfigureKestrel(server => server.Limits.MaxRequestBodySize = fileOptions.RequestMaxBytes);
builder.Services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = fileOptions.RequestMaxBytes);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages();
builder.Services.AddLocalSecurity();

var app = builder.Build();

if (await LocalCommands.TryRunAsync(app, args)) return;

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseAuthentication();
app.UseStatusCodePages(async status =>
{
    if (status.HttpContext.Response.StatusCode == StatusCodes.Status413PayloadTooLarge)
        await status.HttpContext.Response.WriteAsync("El archivo supera la capacidad técnica de carga. Conservá el original y solicitá al administrador revisar los límites del servidor.");
});
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets().AllowAnonymous();
app.MapRazorPages();
app.MapGet("/negocio/logo/{version:guid}", async (Guid version, IBusinessSettingsService business, CancellationToken ct) =>
{
    var png = await business.GetLogoAsync(version, ct);
    return png is null ? Results.NotFound() : Results.File(png, "image/png");
}).AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;

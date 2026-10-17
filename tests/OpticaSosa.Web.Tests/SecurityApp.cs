using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class SecurityApp : WebApplicationFactory<OpticaSosa.Web.Components.App>
{
    internal const string TestPassword = "Ficticia-Para-Pruebas-938!";
    internal string Root { get; }
    private readonly bool preserveData;
    private readonly Action<IServiceCollection>? configureServices;

    public SecurityApp(string? root = null, bool preserveData = false, Action<IServiceCollection>? configureServices = null)
    {
        Root = Path.GetFullPath(root ?? Path.Combine(Path.GetTempPath(), "OpticaSosa.WebTests", Guid.NewGuid().ToString("N")));
        var target = new DirectoryInfo(Root);
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OpticaSosa.WebTests"));
        if (!string.Equals(target.Parent?.FullName, parent, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(target.Name, "N", out _)) throw new InvalidOperationException("Directorio de pruebas inválido.");
        this.preserveData = preserveData;
        this.configureServices = configureServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Storage:DataDirectory", Path.Combine(Root, "private"));
        builder.ConfigureServices(services => configureServices?.Invoke(services));
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        // Fallar antes de migrar si el host no adoptó el directorio de prueba.
        Assert.Equal(Path.Combine(Root, "private"), scope.ServiceProvider.GetRequiredService<StoragePaths>().DataDirectory);
        var provisioning = scope.ServiceProvider.GetRequiredService<LocalProvisioning>();
        await provisioning.InitializeAsync();
        Assert.True((await provisioning.CreateFirstAdminAsync("admin.ficticio", TestPassword)).Succeeded);
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var (name, role) in new[] { ("ventas.ficticio", Roles.Sales), ("profesional.ficticio", Roles.Professional) })
        {
            var user = new ApplicationUser { UserName = name };
            Assert.True((await manager.CreateAsync(user, TestPassword)).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        }
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (preserveData) return;
        var target = new DirectoryInfo(Path.GetFullPath(Root));
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OpticaSosa.WebTests"));
        if (!string.Equals(target.Parent?.FullName, parent, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(target.Name, "N", out _)) throw new InvalidOperationException("Ruta de limpieza inválida.");
        if (target.Exists) target.Delete(recursive: true);
    }
}

using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Backups;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class BackupFolderTests
{
    private static BackupFolderService Service(BackupTestContext test, ClinicalScenario actor, bool create = false) =>
        new(test.App.Services.GetRequiredService<ServerBackupFolders>(), actor.Actor,
            Options.Create(new BackupRuntimeOptions { FolderCreationAuthorized = create }), actor.Factory);

    [Fact]
    public async Task BrowserListsFoldersOnlyPaginatesAndDoesNotExposeBackupContents()
    {
        await using var test = new BackupTestContext(authorized: false); await test.InitializeAsync();
        using var admin = await ClinicalScenario.OpenAsync(test.App, "admin.ficticio"); var service = Service(test, admin);
        var root = test.Targets.ServerRoot;
        Directory.CreateDirectory(Path.Combine(root, "Windows")); Directory.CreateDirectory(Path.Combine(root, ".privado"));
        Directory.CreateDirectory(Path.Combine(root, "Copia-clinica"));
        await File.WriteAllTextAsync(Path.Combine(root, "Copia-clinica", "optica.db"), "Base ficticia, no válida");
        await File.WriteAllTextAsync(Path.Combine(root, "documento-ficticio.txt"), "No se debe listar");
        for (var i = 0; i < 55; i++) Directory.CreateDirectory(Path.Combine(root, $"Carpeta-{i:00}"));
        var first = await service.BrowseAsync(test.Targets.VolumeId);
        Assert.Equal(50, first.Items.Count); Assert.True(first.HasMore); Assert.False(first.CanCreate);
        var last = await service.BrowseAsync(test.Targets.VolumeId, page: 2);
        Assert.Equal(6, last.Items.Count); Assert.False(last.HasMore);
        var names = first.Items.Concat(last.Items).Select(x => x.Name).ToArray();
        Assert.DoesNotContain("Windows", names); Assert.DoesNotContain(".privado", names);
        Assert.DoesNotContain("Copia-clinica", names); Assert.DoesNotContain("documento-ficticio.txt", names);
        var selected = await service.SelectAsync(test.Targets.VolumeId, "Respaldos");
        Assert.Equal(Path.Combine(root, "Respaldos"), selected.FullPath);
        Assert.False(Directory.Exists(test.Targets.Root));
    }

    [Theory]
    [InlineData("../fuera")] [InlineData("..\\fuera")] [InlineData("D:\\Optica")]
    [InlineData("/Optica")] [InlineData("Respaldos//otro")] [InlineData("Windows")]
    [InlineData("$Recycle.Bin")] [InlineData("")]
    public async Task ManipulatedProtectedAndRootDestinationsAreRejected(string folder)
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync();
        using var admin = await ClinicalScenario.OpenAsync(test.App, "admin.ficticio");
        var error = await Record.ExceptionAsync(() => Service(test, admin).SelectAsync(test.Targets.VolumeId, folder));
        Assert.True(error is IOException or ArgumentException);
    }

    [Fact]
    public async Task JunctionCannotBeListedTraversedOrSelected()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync();
        var outside = Path.Combine(test.App.Root, "outside-volume-ficticio"); Directory.CreateDirectory(outside);
        var link = Path.Combine(test.Targets.ServerRoot, "Escape");
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, outside }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!; await process.WaitForExitAsync(); Assert.Equal(0, process.ExitCode);
        try
        {
            using var admin = await ClinicalScenario.OpenAsync(test.App, "admin.ficticio"); var service = Service(test, admin);
            Assert.DoesNotContain((await service.BrowseAsync(test.Targets.VolumeId)).Items, x => x.Name == "Escape");
            await Assert.ThrowsAsync<IOException>(() => service.BrowseAsync(test.Targets.VolumeId, "Escape"));
            await Assert.ThrowsAsync<IOException>(() => service.SelectAsync(test.Targets.VolumeId, "Escape"));
        }
        finally
        {
            // Retirar sólo la unión propia, sin recursión ni eliminación de su destino.
            Assert.StartsWith(test.App.Root + Path.DirectorySeparatorChar, Path.GetFullPath(link), StringComparison.OrdinalIgnoreCase);
            Directory.Delete(link, recursive: false);
        }
    }

    [Fact]
    public async Task CreationNeedsAuthorizationConfirmationPrivateParentAndDoesNotOverwrite()
    {
        await using var test = new BackupTestContext(authorized: false); await test.InitializeAsync();
        using var admin = await ClinicalScenario.OpenAsync(test.App, "admin.ficticio");
        Assert.False((await Service(test, admin).CreateAsync(test.Targets.VolumeId, "Respaldos", "Nueva", true)).Succeeded);
        var allowed = Service(test, admin, create: true);
        Assert.False((await allowed.CreateAsync(test.Targets.VolumeId, "Respaldos", "Nueva", false)).Succeeded);
        Assert.True((await allowed.BrowseAsync(test.Targets.VolumeId, "Respaldos")).CanCreate);
        Assert.True((await allowed.CreateAsync(test.Targets.VolumeId, "Respaldos", "Nueva", true)).Succeeded);
        var child = Path.Combine(test.Targets.ServerRoot, "Respaldos", "Nueva");
        var marker = Path.Combine(child, "testigo-ficticio.txt"); await File.WriteAllTextAsync(marker, "Conservar");
        Assert.False((await allowed.CreateAsync(test.Targets.VolumeId, "Respaldos", "Nueva", true)).Succeeded);
        Assert.Equal("Conservar", await File.ReadAllTextAsync(marker));
        Assert.False((await allowed.CreateAsync(test.Targets.VolumeId, "", "Nueva", true)).Succeeded);
        Assert.False((await allowed.CreateAsync(test.Targets.VolumeId, "Respaldos", "../escape", true)).Succeeded);
        Assert.True(await admin.Db.AuditEntries.AnyAsync(x => x.Action == "backup.folder.created"));
        Assert.False(Directory.Exists(test.Targets.Root));
        Assert.False((await test.Coordinator.OverviewAsync(default)).Configuration.Enabled);
    }

    [Theory]
    [InlineData("ventas.ficticio")] [InlineData("profesional.ficticio")]
    public async Task NonAdministratorsCannotBrowseSelectOrCreate(string user)
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync();
        using var actor = await ClinicalScenario.OpenAsync(test.App, user); var service = Service(test, actor, create: true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.BrowseAsync(test.Targets.VolumeId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SelectAsync(test.Targets.VolumeId, "Respaldos"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(test.Targets.VolumeId, "Respaldos", "Nueva", true));
    }

    [Fact]
    public async Task CreationDoesNotChangePermissionsOfPublicParent()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Las pruebas de permisos del servidor requieren Windows.");
        await using var test = new BackupTestContext(); await test.InitializeAsync();
        var parent = new DirectoryInfo(Path.Combine(test.Targets.ServerRoot, "Publica-ficticia")); parent.Create();
        var acl = parent.GetAccessControl();
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        parent.SetAccessControl(acl);
        var before = parent.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access);
        using var admin = await ClinicalScenario.OpenAsync(test.App, "admin.ficticio"); var service = Service(test, admin, create: true);
        Assert.False((await service.BrowseAsync(test.Targets.VolumeId, parent.Name)).CanCreate);
        Assert.False((await service.CreateAsync(test.Targets.VolumeId, parent.Name, "Nueva", true)).Succeeded);
        Assert.False(Directory.Exists(Path.Combine(parent.FullName, "Nueva")));
        Assert.Equal(before, parent.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access));
    }

    [Fact]
    public async Task DisconnectAndDifferentVolumeAreRejectedWithoutFallback()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync();
        using var admin = await ClinicalScenario.OpenAsync(test.App, "admin.ficticio"); var service = Service(test, admin);
        await Assert.ThrowsAsync<IOException>(() => service.BrowseAsync("otro-volumen"));
        test.Targets.Connected = false;
        await Assert.ThrowsAsync<IOException>(() => service.BrowseAsync(test.Targets.VolumeId));
        await Assert.ThrowsAsync<IOException>(() => service.SelectAsync(test.Targets.VolumeId, "Respaldos"));
        Assert.False(Directory.Exists(test.Targets.Root));
    }
}

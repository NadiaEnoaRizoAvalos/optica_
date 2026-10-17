using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Infrastructure.Backups;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class ViewerPortableKitTests
{
    [Fact]
    public async Task FictitiousPackageIsSignedVerifiableAndNeverExportsToAnOperationalDirectory()
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        var root = Path.Combine(fixture.Source.Root, "package-test"); Directory.CreateDirectory(root);
        await ExportAsync(fixture, root);
        var authenticity = fixture.Source.Services.GetRequiredService<BackupAuthenticity>();
        var manifest = Path.Combine(root, "complete.json");
        await authenticity.VerifyAsync(root, await File.ReadAllBytesAsync(manifest), 3, default);
        using (var document = JsonDocument.Parse(await File.ReadAllBytesAsync(manifest)))
            foreach (var file in document.RootElement.GetProperty("Files").EnumerateObject())
            {
                await using var input = File.OpenRead(BackupFileSafety.Child(root, file.Name));
                Assert.Equal(file.Value.GetString(), Convert.ToHexString(await SHA256.HashDataAsync(input)));
            }
        await File.AppendAllTextAsync(manifest, "ALTERACION FICTICIA");
        await Assert.ThrowsAsync<InvalidDataException>(() => authenticity.VerifyAsync(root, File.ReadAllBytes(manifest), 3, default));

        var export = Environment.GetEnvironmentVariable("OPTICA_VIEWER_EXPORT_ROOT");
        if (string.IsNullOrWhiteSpace(export)) return;
        var project = new DirectoryInfo(AppContext.BaseDirectory);
        while (project is not null && !File.Exists(Path.Combine(project.FullName, "OpticaSosa.sln"))) project = project.Parent;
        Assert.NotNull(project);
        var folder = new DirectoryInfo(Path.GetFullPath(export));
        Assert.Equal(Path.Combine(project.FullName, "data"), folder.Parent?.FullName, ignoreCase: true);
        Assert.StartsWith("visor-emergencia-52b-", folder.Name);
        BackupFileSafety.RejectLinks(folder.FullName);
        Assert.True(folder.Exists); Assert.False(File.Exists(Path.Combine(folder.FullName, "complete.json")));
        Assert.True(File.Exists(Path.Combine(folder.FullName, "app", "OpticaSosa.Viewer.dll")));
        Assert.True(File.Exists(Path.Combine(folder.FullName, "runtime", "dotnet.exe")));
        await ExportAsync(fixture, folder.FullName);
        await File.WriteAllTextAsync(Path.Combine(project.FullName, "data", "latest-viewer-test-parent.json"),
            JsonSerializer.Serialize(new { Parent = fixture.Source.Root, Fictitious = true }));
    }

    private static async Task ExportAsync(ViewerFixture fixture, string root)
    {
        foreach (var file in Directory.GetFiles(fixture.Backup, "*", SearchOption.AllDirectories))
        {
            var target = BackupFileSafety.Child(Path.Combine(root, "backup"), Path.GetRelativePath(fixture.Backup, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target, overwrite: false);
        }
        await File.WriteAllTextAsync(Path.Combine(root, "firmante-publico.txt"), fixture.Fingerprint);
        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            await using var input = File.OpenRead(file);
            hashes[Path.GetRelativePath(root, file).Replace('\\', '/')] = Convert.ToHexString(await SHA256.HashDataAsync(input));
        }
        await File.WriteAllTextAsync(Path.Combine(root, "complete.json"),
            JsonSerializer.Serialize(new { Format = 1, Purpose = "VISOR-FICTICIO-52B", Files = hashes }));
        await fixture.Source.Services.GetRequiredService<BackupAuthenticity>().SignAsync(root, default);
    }
}

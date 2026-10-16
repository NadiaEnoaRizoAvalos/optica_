using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class StoragePathsTests
{
    private static string Root => Path.Combine(Path.GetTempPath(), "OpticaSosa.PathTests");

    [Theory]
    [InlineData("wwwroot")]
    [InlineData("wwwroot/private")]
    [InlineData("wwwroot/../wwwroot/private")]
    [InlineData(".")]
    public void RejectsPathsOverlappingPublicFiles(string relative)
    {
        Assert.Throws<ArgumentException>(() => new StoragePaths(
            Path.Combine(Root, relative), Path.Combine(Root, "wwwroot")));
    }

    [Fact]
    public void RejectsRelativeDataDirectory()
    {
        Assert.Throws<ArgumentException>(() => new StoragePaths("data", Path.Combine(Root, "wwwroot")));
    }

    [Fact]
    public void AllowsPrivateSiblingWithoutCreatingFiles()
    {
        var root = Path.Combine(Root, Guid.NewGuid().ToString("N"));
        var paths = new StoragePaths(Path.Combine(root, "wwwroot-private"), Path.Combine(root, "wwwroot"));
        Assert.Equal(Path.Combine(root, "wwwroot-private", "optica.db"), paths.DatabaseFile);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void RejectsNetworkPathOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Throws<ArgumentException>(() => new StoragePaths(@"\\server\share\data", Path.Combine(Root, "wwwroot")));
    }
}

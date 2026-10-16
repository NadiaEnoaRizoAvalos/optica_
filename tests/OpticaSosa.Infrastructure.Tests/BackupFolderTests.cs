using OpticaSosa.Domain.Backups;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class BackupFolderTests
{
    [Fact]
    public void RelativeFoldersArePortableAndNotTiedToAnOpticOrDriveLetter() =>
        Assert.Equal("MiOptica/Respaldos", BackupFolder.Normalize("MiOptica\\Respaldos"));

    [Theory]
    [InlineData("")] [InlineData("D:\\Optica\\Respaldos")] [InlineData("../otra")]
    [InlineData("/absoluta")] [InlineData("carpeta//otra")] [InlineData("CON")]
    [InlineData("carpeta/otra.")] [InlineData("carpeta/otra ")]
    public void UnsafeFolderNamesAreRejected(string value) => Assert.Throws<ArgumentException>(() => BackupFolder.Normalize(value));
}

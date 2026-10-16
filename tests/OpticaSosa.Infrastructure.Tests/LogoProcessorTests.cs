using OpticaSosa.Application.Business;
using OpticaSosa.Infrastructure.Business;
using SkiaSharp;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class LogoProcessorTests
{
    [Fact]
    public void ValidImageIsReencodedAsBoundedPngWithoutOriginalTrailingContent()
    {
        using var bitmap = new SKBitmap(800, 400); bitmap.Erase(SKColors.Teal);
        using var image = SKImage.FromBitmap(bitmap);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        var original = jpeg.ToArray().Concat(System.Text.Encoding.UTF8.GetBytes("synthetic trailing marker")).ToArray();
        var clean = LogoProcessor.Normalize(new(original, "../../logo.jpg", "image/jpeg"));
        using var data = SKData.CreateCopy(clean);
        using var codec = SKCodec.Create(data);
        Assert.Equal(SKEncodedImageFormat.Png, codec.EncodedFormat);
        Assert.Equal(512, codec.Info.Width); Assert.Equal(256, codec.Info.Height);
        Assert.DoesNotContain("synthetic trailing marker", System.Text.Encoding.UTF8.GetString(clean));
    }

    [Theory]
    [InlineData("logo.svg", "image/svg+xml", "<svg><script>alert(1)</script></svg>")]
    [InlineData("logo.png", "image/png", "not an image")]
    [InlineData("logo.jpg", "image/jpeg", "<html>synthetic</html>")]
    public void InvalidOrActiveContentIsRejected(string name, string type, string content) =>
        Assert.Throws<ArgumentException>(() => LogoProcessor.Normalize(new(System.Text.Encoding.UTF8.GetBytes(content), name, type)));

    [Fact]
    public void OversizedOrMislabeledImagesAreRejectedBeforeStorage()
    {
        Assert.Throws<ArgumentException>(() => LogoProcessor.Normalize(new(new byte[LogoProcessor.MaximumBytes + 1], "logo.png", "image/png")));
        using var bitmap = new SKBitmap(1, 2049); bitmap.Erase(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        Assert.Throws<ArgumentException>(() => LogoProcessor.Normalize(new(png.ToArray(), "logo.png", "image/png")));
        using var small = new SKBitmap(2, 2); small.Erase(SKColors.Blue);
        using var smallImage = SKImage.FromBitmap(small);
        using var smallPng = smallImage.Encode(SKEncodedImageFormat.Png, 100);
        Assert.Throws<ArgumentException>(() => LogoProcessor.Normalize(new(smallPng.ToArray(), "logo.jpg", "image/jpeg")));
    }
}

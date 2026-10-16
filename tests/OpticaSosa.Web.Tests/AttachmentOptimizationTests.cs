using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Infrastructure.Clinical;
using SkiaSharp;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class AttachmentOptimizationTests
{
    [Fact]
    public async Task PngOptimizationPreservesEveryDecodedPixelAndDoesNotStoreOrReplaceTheOriginal()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        using var bitmap = new SKBitmap(100, 100); bitmap.Erase(SKColors.White);
        for (int x = 10; x < 90; x++) bitmap.SetPixel(x, 50, SKColors.Black);
        using var pixels = bitmap.PeekPixels();
        using var encoded = pixels.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.None, 0));
        Assert.NotNull(encoded);
        var original = encoded.ToArray(); var unchanged = original.ToArray();
        using var input = new MemoryStream(original);
        var result = await test.Scenario.Attachments.OptimizeAsync(test.CustomerId, test.RecordId,
            new(Guid.NewGuid(), Guid.Empty, Guid.Empty, "receta-ficticia.png", "image/png", original.Length, input));
        Assert.NotNull(result.Bytes); Assert.True(result.Bytes.Length < original.Length); Assert.Equal(unchanged, original);
        using var restored = SKBitmap.Decode(result.Bytes);
        Assert.Equal(bitmap.Width, restored.Width); Assert.Equal(bitmap.Height, restored.Height);
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++) Assert.Equal(bitmap.GetPixel(x, y), restored.GetPixel(x, y));
        Assert.Empty(await test.Scenario.Db.ClinicalAttachments.ToListAsync());
    }

    [Theory]
    [InlineData("receta-ficticia.jpg", "image/jpeg")]
    [InlineData("receta-ficticia.pdf", "application/pdf")]
    public async Task DocumentsWithoutVerifiedLosslessOptimizationAreNotRecompressed(string name, string mime)
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        using var input = new MemoryStream(AttachmentTestContext.Pdf());
        var result = await test.Scenario.Attachments.OptimizeAsync(test.CustomerId, test.RecordId,
            new(Guid.NewGuid(), Guid.Empty, Guid.Empty, name, mime, input.Length, input));
        Assert.Null(result.Bytes); Assert.Contains("No se recomprimen", result.Error!);
        Assert.Empty(await test.Scenario.Db.ClinicalAttachments.ToListAsync());
    }
}

using OpticaSosa.Application.Business;
using SkiaSharp;

namespace OpticaSosa.Infrastructure.Business;

public static class LogoProcessor
{
    public const int MaximumBytes = LogoUpload.MaximumBytes;

    public static byte[] Normalize(LogoUpload upload)
    {
        if (upload.Bytes.Length is 0 or > MaximumBytes)
            throw new ArgumentException("El logo debe ocupar como máximo 2 MB.");
        var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
        using var encoded = SKData.CreateCopy(upload.Bytes);
        using var codec = SKCodec.Create(encoded);
        if (codec is null || codec.Info.Width is <= 0 or > 2048 || codec.Info.Height is <= 0 or > 2048 || codec.FrameCount > 1)
            throw new ArgumentException("Usá una imagen PNG o JPEG estática y válida, de hasta 2048 × 2048 píxeles.");
        var validType = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Png => extension == ".png" && upload.ContentType == "image/png",
            SKEncodedImageFormat.Jpeg => extension is ".jpg" or ".jpeg" && upload.ContentType == "image/jpeg",
            _ => false
        };
        if (!validType) throw new ArgumentException("El contenido, extensión y tipo del logo deben corresponder a PNG o JPEG.");
        using var decoded = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
            throw new ArgumentException("La imagen está incompleta o no se pudo decodificar.");
        var scale = Math.Min(1d, 512d / Math.Max(decoded.Width, decoded.Height));
        var width = Math.Max(1, (int)Math.Round(decoded.Width * scale));
        var height = Math.Max(1, (int)Math.Round(decoded.Height * scale));
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.Transparent);
        using var image = SKImage.FromBitmap(decoded);
        surface.Canvas.DrawImage(image, new SKRect(0, 0, width, height),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        using var cleanImage = surface.Snapshot();
        using var png = cleanImage.Encode(SKEncodedImageFormat.Png, 100);
        var result = png.ToArray();
        if (result.Length > MaximumBytes) throw new ArgumentException("El logo procesado supera el tamaño permitido.");
        return result;
    }
}

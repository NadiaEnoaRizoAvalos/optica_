using Microsoft.Extensions.Options;
using OpticaSosa.Application.Clinical;
using SkiaSharp;
using UglyToad.PdfPig;

namespace OpticaSosa.Infrastructure.Clinical;

public sealed class ClinicalFileValidation(IOptions<ClinicalFileOptions> options)
{
    public static string SafeName(string original)
    {
        var name = original.Replace('\\', '/').Split('/').Last().Trim();
        if (name.Length is 0 or > 180 || name.Any(c => char.IsControl(c)) || name.Contains(':'))
            throw new ArgumentException("El nombre del archivo no es válido; usá hasta 180 caracteres sin caracteres de control.");
        return name;
    }

    public static string ExpectedType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".pdf" => "application/pdf",
        ".heic" or ".heif" => throw new ArgumentException("HEIC/HEIF todavía no dispone de conversión verificada en este servidor. Exportá una copia JPG/PNG desde el celular conservando el original."),
        _ => throw new ArgumentException("Usá JPG/JPEG, PNG o PDF.")
    };

    public async Task<byte[]> ReadAsync(AttachmentUpload upload, long limit, CancellationToken ct)
    {
        if (upload.Length <= 0 || upload.Length > limit)
            throw new ArgumentException($"El archivo supera el límite permitido de {limit / ClinicalFileOptions.MiB} MB o está vacío. Elegí otro archivo, probá la optimización PNG sin pérdida o solicitá un ajuste administrativo.");
        using var buffer = new MemoryStream();
        var block = new byte[81920];
        long count = 0;
        int read;
        while ((read = await upload.Content.ReadAsync(block, ct)) != 0)
        {
            count += read;
            if (count > limit) throw new ArgumentException("El contenido supera el límite permitido; no se guardó el archivo.");
            await buffer.WriteAsync(block.AsMemory(0, read), ct);
        }
        if (count != upload.Length) throw new ArgumentException("La carga está incompleta o su tamaño no coincide.");
        return buffer.ToArray();
    }

    public void Validate(byte[] bytes, string contentType, string suppliedType)
    {
        if (!string.Equals(contentType, suppliedType, StringComparison.OrdinalIgnoreCase) &&
            suppliedType is not ("" or "application/octet-stream"))
            throw new ArgumentException("El tipo declarado no coincide con la extensión del archivo.");
        if (contentType == "application/pdf")
        {
            try
            {
                if (!bytes.AsSpan().StartsWith("%PDF-"u8)) throw new ArgumentException();
                using var document = PdfDocument.Open(bytes, new ParsingOptions { UseLenientParsing = false, MaxStackDepth = 128 });
                if (document.NumberOfPages < 1) throw new ArgumentException();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
            { throw new ArgumentException("El PDF no se puede validar: está dañado, vacío o protegido. No se modificó el original."); }
            return;
        }
        using var encoded = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(encoded);
        var format = contentType == "image/png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
        if (codec is null || codec.EncodedFormat != format || codec.FrameCount > 1 || codec.Info.Width <= 0 || codec.Info.Height <= 0 ||
            (long)codec.Info.Width * codec.Info.Height > options.Value.MaximumImagePixels)
            throw new ArgumentException("La imagen no coincide con su tipo, no es estática o supera el límite técnico de píxeles configurado.");
        using var decoded = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        if (decoded.GetPixels() == IntPtr.Zero || codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
            throw new ArgumentException("La imagen está incompleta o dañada; elegí otro archivo. No se guardó una copia parcial.");
    }
}

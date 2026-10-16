namespace OpticaSosa.Application.Clinical;

public sealed class ClinicalFileOptions
{
    public const string Section = "ClinicalFiles";
    public const long MiB = 1024L * 1024L;
    public int RequestMaxMiB { get; set; } = 128;
    public long MaximumImagePixels { get; set; } = 40_000_000;
    public int ReserveDiskMiB { get; set; } = 512;
    public long RequestMaxBytes => RequestMaxMiB * MiB;
    public bool Valid() => RequestMaxMiB is >= 64 and <= 512 && MaximumImagePixels is > 0 and <= 100_000_000 && ReserveDiskMiB >= 0;
}

public static class HistoryPaging
{
    public const int Size = 25;
    public static bool IsValid(int page) => page >= 1 && ((long)page - 1) * Size <= int.MaxValue;
    public static int Offset(int page) => IsValid(page) ? (page - 1) * Size : throw new ArgumentException("Página fuera de la capacidad técnica de consulta.");
}

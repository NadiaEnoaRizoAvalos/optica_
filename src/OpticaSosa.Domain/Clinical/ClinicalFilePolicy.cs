namespace OpticaSosa.Domain.Clinical;

public sealed class ClinicalFilePolicy
{
    public const int InitialPhotoMiB = 25;
    public const int InitialPdfMiB = 50;
    public int Id { get; private set; } = 1;
    public int PhotoMiB { get; private set; } = InitialPhotoMiB;
    public int PdfMiB { get; private set; } = InitialPdfMiB;
    public Guid Version { get; private set; }
    public void Update(int photoMiB, int pdfMiB)
    {
        if (photoMiB < 1 || pdfMiB < 1) throw new ArgumentException("Los límites deben ser positivos.");
        PhotoMiB = photoMiB;
        PdfMiB = pdfMiB;
        Version = Guid.NewGuid();
    }
}

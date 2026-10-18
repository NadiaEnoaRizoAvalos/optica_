using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Clinical;

namespace OpticaSosa.Domain.Technical;

public sealed class TechnicalPublication
{
    public Guid Id { get; private set; }
    public Guid SourceRevisionId { get; private set; }
    public TechnicalRequest Request { get; private set; } = null!;
    public EyeValues Right { get; private set; } = new();
    public EyeValues Left { get; private set; } = new();
    public int? PupillaryDistanceHundredths { get; private set; }
    public Guid? PreviousId { get; private set; }
    public Guid? SupersededById { get; private set; }
    public string? CorrectionReason { get; private set; }
    public string PublishedByUserId { get; private set; } = "";
    public DateTime PublishedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevokedByUserId { get; private set; }
    public string? RevocationReason { get; private set; }
    public Guid Version { get; private set; }

    private TechnicalPublication() { }
    public TechnicalPublication(Guid id, Guid revisionId, TechnicalRequest request, ClinicalData source,
        string actor, DateTime utcNow, Guid? previousId = null, string? correctionReason = null)
    {
        if (id == Guid.Empty || revisionId == Guid.Empty || previousId == Guid.Empty || previousId == id ||
            string.IsNullOrWhiteSpace(actor) || utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("Identificación o fecha inválida.");
        Id = id; SourceRevisionId = revisionId;
        Request = new(request.Purpose, request.IncludeRight, request.IncludeLeft, request.Product, request.ProductId,
            request.Material, request.RefractiveIndex, request.Treatment, request.Mounting, request.RightContact, request.LeftContact);
        PreviousId = previousId;
        CorrectionReason = previousId is not null ? CatalogText.Required(correctionReason, 500, "el motivo de corrección") : null;
        Right = request.IncludeRight ? ValidateEye(source.Right) : new();
        Left = request.IncludeLeft ? ValidateEye(source.Left) : new();
        PupillaryDistanceHundredths = request.Purpose == TechnicalPurpose.SpectacleLenses
            ? EyeMeasurement.Scale(source.PupillaryDistance, 100, "distancia pupilar") : null;
        PublishedByUserId = actor; PublishedAtUtc = utcNow; Version = Guid.NewGuid();
    }

    private static EyeValues ValidateEye(EyeValues values)
    {
        if (values.Sphere is null) throw new ArgumentException("El ojo incluido necesita esfera explícita; cero es un valor válido.");
        if (values.Cylinder is { } cylinder && cylinder != 0 && values.Axis is null)
            throw new ArgumentException("Un cilindro distinto de cero necesita eje.");
        if (values.Prism is { } prism && prism != 0 && string.IsNullOrWhiteSpace(values.Base))
            throw new ArgumentException("Un prisma distinto de cero necesita base.");
        // Reutilizar la validación exacta de graduaciones; no convertir ni calcular valores clínicos.
        return new EyeMeasurement(Guid.NewGuid(), EyeSide.Right, values).ToValues();
    }
    public void Supersede(Guid successor)
    {
        if (successor == Guid.Empty || successor == Id || SupersededById is not null)
            throw new InvalidOperationException("La publicación ya fue reemplazada o la referencia no es válida.");
        SupersededById = successor; Version = Guid.NewGuid();
    }
    public PublicationStatus Status(int sourceNumber, int currentNumber, bool customerActive) =>
        RevokedAtUtc is not null ? PublicationStatus.Revoked : SupersededById is not null ? PublicationStatus.Superseded
            : sourceNumber != currentNumber ? PublicationStatus.SourceChanged : !customerActive ? PublicationStatus.CustomerInactive : PublicationStatus.Current;
    public void Revoke(string actor, DateTime utcNow, string reason)
    {
        if (string.IsNullOrWhiteSpace(actor) || utcNow.Kind != DateTimeKind.Utc || RevokedAtUtc is not null)
            throw new InvalidOperationException("La publicación ya está revocada o la solicitud no es válida.");
        RevocationReason = CatalogText.Required(reason, 500, "el motivo de revocación");
        RevokedAtUtc = utcNow; RevokedByUserId = actor; Version = Guid.NewGuid();
    }
}

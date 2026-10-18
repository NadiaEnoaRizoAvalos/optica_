using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Technical;

namespace OpticaSosa.Application.Technical;

public sealed class TechnicalPublicationInput
{
    public Guid SubmissionId { get; set; } = Guid.NewGuid();
    public Guid SourceRevisionId { get; set; }
    public Guid RecordVersion { get; set; }
    public bool Confirmed { get; set; }
    public TechnicalPurpose Purpose { get; set; }
    public bool IncludeRight { get; set; }
    public bool IncludeLeft { get; set; }
    public string Product { get; set; } = "";
    public Guid? ProductId { get; set; }
    public string? Material { get; set; }
    public decimal? RefractiveIndex { get; set; }
    public string? Treatment { get; set; }
    public MountingSpecification? Mounting { get; set; }
    public ContactSelection? RightContact { get; set; }
    public ContactSelection? LeftContact { get; set; }
    public Guid? CorrectsId { get; set; }
    public Guid CorrectsVersion { get; set; }
    public string? CorrectionReason { get; set; }
    public TechnicalRequest ToRequest() => new(Purpose, IncludeRight, IncludeLeft, Product, ProductId, Material, RefractiveIndex, Treatment,
        Mounting, RightContact, LeftContact);
}

public sealed record TechnicalPublicationDetails(Guid Id, Guid CustomerId, Guid RecordId, Guid RevisionId, int RevisionNumber,
    TechnicalRequest Request, EyeValues Right, EyeValues Left, decimal? PupillaryDistance,
    string PublishedByUserId, DateTime PublishedAtUtc, Guid Version, PublicationStatus Status,
    Guid? PreviousId, Guid? SupersededById, string? CorrectionReason, DateTime? RevokedAtUtc, string? RevocationReason);
public sealed record TechnicalPublicationPage(IReadOnlyList<TechnicalPublicationDetails> Items, int Page, bool HasMore);
public enum TechnicalResultCode { Success, Invalid, NotFound, Conflict, Obsolete }
public sealed record TechnicalResult(TechnicalResultCode Code, Guid? Id = null, string? Error = null)
{
    public bool Succeeded => Code == TechnicalResultCode.Success;
}

// Publicación y consulta profesional. Ventas recibirá una proyección ligada a su pedido,
// no acceso general a las publicaciones de un cliente ni a la historia clínica.
public interface ITechnicalPublicationService
{
    Task<TechnicalPublicationPage> ListAsync(Guid customerId, Guid recordId, int page = 1, CancellationToken ct = default);
    Task<TechnicalPublicationDetails?> GetAsync(Guid customerId, Guid publicationId, CancellationToken ct = default);
    Task<TechnicalResult> PublishAsync(Guid customerId, Guid recordId, TechnicalPublicationInput input, CancellationToken ct = default);
    Task<TechnicalResult> RevokeAsync(Guid customerId, Guid publicationId, Guid version, string reason, CancellationToken ct = default);
}

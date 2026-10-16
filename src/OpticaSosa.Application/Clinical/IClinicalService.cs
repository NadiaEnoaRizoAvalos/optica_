using System.ComponentModel.DataAnnotations;
using OpticaSosa.Domain.Clinical;

namespace OpticaSosa.Application.Clinical;

public sealed class EyeInput
{
    public decimal? Sphere { get; set; }
    public decimal? Cylinder { get; set; }
    public int? Axis { get; set; }
    public decimal? Addition { get; set; }
    public decimal? Prism { get; set; }
    [StringLength(60)] public string? Base { get; set; }
    public EyeValues ToValues() => new(Sphere, Cylinder, Axis, Addition, Prism, Base);
    public static EyeInput From(EyeValues v) => new() { Sphere = v.Sphere, Cylinder = v.Cylinder, Axis = v.Axis,
        Addition = v.Addition, Prism = v.Prism, Base = v.Base };
}

public sealed class ClinicalInput
{
    public Guid SubmissionId { get; set; } = Guid.NewGuid();
    public Guid Version { get; set; }
    public Guid? VisitId { get; set; }
    public ClinicalKind Kind { get; set; }
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public Guid ProfessionalId { get; set; }
    [Required, StringLength(200)] public string Source { get; set; } = "";
    [StringLength(4000)] public string? Notes { get; set; }
    public bool IsFollowUp { get; set; }
    [StringLength(2000)] public string? ReportedSymptoms { get; set; }
    public ReportedBy ReportedBy { get; set; } = ReportedBy.Patient;
    public decimal? PupillaryDistance { get; set; }
    public EyeInput Right { get; set; } = new();
    public EyeInput Left { get; set; } = new();
    [StringLength(500)] public string? CorrectionReason { get; set; }
    public ClinicalData ToData() => new(Date, ProfessionalId, Source, Notes, PupillaryDistance, Right.ToValues(), Left.ToValues(), IsFollowUp, ReportedSymptoms, ReportedBy);
}

public sealed record ClinicalSummary(Guid Id, ClinicalKind Kind, DateOnly Date, string ProfessionalName, int RevisionNumber);
public sealed record ClinicalHistory(IReadOnlyList<ClinicalSummary> Items, int Page, bool HasMore);
public sealed record RevisionSummary(int Number, DateTime RecordedAtUtc, string RecordedByUserId, string? CorrectionReason);
public sealed record ClinicalDetails(Guid Id, Guid CustomerId, Guid? VisitId, ClinicalKind Kind, Guid Version,
    int LatestNumber, Guid RevisionId, int Number, ClinicalData Data, string ProfessionalName,
    string? ProfessionalRegistration, string? ProfessionalJurisdiction, string RecordedByUserId, DateTime RecordedAtUtc,
    string? CorrectionReason, IReadOnlyList<RevisionSummary> History, int HistoryPage, bool HasOlderRevisions);

public interface IClinicalService
{
    Task<ClinicalHistory> ListAsync(Guid customerId, int page = 1, CancellationToken ct = default);
    Task<ClinicalDetails?> GetAsync(Guid customerId, Guid id, int? revision = null, int historyPage = 1, CancellationToken ct = default);
    Task<ClinicalResult> CreateAsync(Guid customerId, ClinicalInput input, CancellationToken ct = default);
    Task<ClinicalResult> CorrectAsync(Guid customerId, Guid id, ClinicalInput input, CancellationToken ct = default);
}

using System.ComponentModel.DataAnnotations;
using OpticaSosa.Domain.Work;
using OpticaSosa.Application.Clinical;

namespace OpticaSosa.Application.Work;

public sealed class WorkInput
{
    public Guid SubmissionId { get; set; } = Guid.NewGuid();
    public Guid Version { get; set; }
    public Guid CustomerId { get; set; }
    public OpticalWorkType Type { get; set; }
    [Required, StringLength(200)] public string Product { get; set; } = "";
    [Required, StringLength(200)] public string Frame { get; set; } = "";
    public Guid? PrescriptionId { get; set; }
}
public sealed class CheckInput
{
    public string Key { get; set; } = "";
    public CheckStatus Status { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}
public sealed class ControlInput
{
    public Guid SubmissionId { get; set; } = Guid.NewGuid();
    public Guid WorkVersion { get; set; }
    public Guid TemplateVersion { get; set; }
    public QualityStage Stage { get; set; }
    public List<CheckInput> Items { get; set; } = [];
    public bool Approve { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
}
public sealed record WorkSummary(Guid Id, Guid CustomerId, string CustomerName, string Product, string Frame, OpticalWorkType Type,
    WorkStatus Status, DateTime CreatedAtUtc, Guid Version, int Round, bool TechnicalApproved, bool DeliveryApproved);
public sealed record WorkPage(IReadOnlyList<WorkSummary> Items, int Page, bool HasMore);
public sealed record CheckDetails(string Key, string Label, bool Applicable, bool Required, CheckStatus Status, string? Notes);
public sealed record ControlDetails(Guid Id, int Round, QualityStage Stage, ControlStatus Status, string ActorId,
    ResponsibleRole Role, DateTime AtUtc, DateTime? ApprovedAtUtc, string? Notes, IReadOnlyList<CheckDetails> Items);
public sealed record TransitionDetails(WorkStatus From, WorkStatus To, string ActorId, DateTime AtUtc, string Action);
public sealed record WorkDetails(WorkSummary Work, Guid? PrescriptionId, int ControlCount, IReadOnlyList<ControlDetails> Controls,
    IReadOnlyList<TransitionDetails> Transitions, int Page, bool HasMoreControls, bool HasMoreTransitions, int? PrescriptionRevisionNumber);
public sealed record TemplateDetails(OpticalWorkType Type, QualityStage Stage, Guid Version, IReadOnlyList<CheckDefinition> Items);
public interface IWorkService
{
    Task<WorkPage> ListAsync(Guid? customerId = null, WorkStatus? status = null, int page = 1, CancellationToken ct = default);
    Task<WorkDetails?> GetAsync(Guid id, int page = 1, CancellationToken ct = default);
    Task<int> CountAsync(Guid customerId, CancellationToken ct = default);
    Task<ClinicalResult> CreateAsync(WorkInput input, CancellationToken ct = default);
    Task<ClinicalResult> EditAsync(Guid id, WorkInput input, CancellationToken ct = default);
    Task<ClinicalResult> TransitionAsync(Guid id, Guid version, string command, CancellationToken ct = default);
    Task<TemplateDetails> TemplateAsync(OpticalWorkType type, QualityStage stage, CancellationToken ct = default);
    Task<ClinicalResult> SaveControlAsync(Guid id, ControlInput input, CancellationToken ct = default);
    Task<ClinicalResult> SaveTemplateAsync(OpticalWorkType type, QualityStage stage, Guid version,
        IReadOnlyList<string> required, CancellationToken ct = default);
}

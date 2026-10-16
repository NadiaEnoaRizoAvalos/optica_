using System.ComponentModel.DataAnnotations;
using OpticaSosa.Domain.Clinical;

namespace OpticaSosa.Application.Clinical;

public sealed class ProfessionalInput
{
    public Guid SubmissionId { get; set; } = Guid.NewGuid();
    public Guid Version { get; set; }
    [Required(ErrorMessage = "Indicá el nombre del profesional."), StringLength(120)]
    public string FullName { get; set; } = "";
    [StringLength(60)] public string? Registration { get; set; }
    [StringLength(80)] public string? Jurisdiction { get; set; }
    public bool IsActive { get; set; } = true;
    public ProfessionalProfile ToProfile() => new(FullName, Registration, Jurisdiction);
}

public sealed record ProfessionalDetails(Guid Id, string FullName, string? Registration, string? Jurisdiction,
    bool IsActive, Guid Version);
public enum ClinicalResultCode { Success, Invalid, NotFound, Conflict }
public sealed record ClinicalResult(ClinicalResultCode Code, Guid? Id = null, string? Error = null)
{
    public bool Succeeded => Code == ClinicalResultCode.Success;
}

public interface IProfessionalService
{
    Task<IReadOnlyList<ProfessionalDetails>> SearchAsync(string? search, CancellationToken ct = default);
    Task<ProfessionalDetails?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ClinicalResult> CreateAsync(ProfessionalInput input, CancellationToken ct = default);
    Task<ClinicalResult> UpdateAsync(Guid id, ProfessionalInput input, CancellationToken ct = default);
}

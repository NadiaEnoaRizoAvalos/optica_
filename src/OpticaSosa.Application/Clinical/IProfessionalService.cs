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
    [StringLength(100)] public string? Specialty { get; set; }
    [StringLength(40), Phone] public string? Phone { get; set; }
    [StringLength(254), EmailAddress] public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public ProfessionalProfile ToProfile() => new(FullName, Registration, Jurisdiction, Specialty, Phone, Email);
}

public sealed record ProfessionalDetails(Guid Id, string FullName, string? Registration, string? Jurisdiction,
    bool IsActive, Guid Version, string? Specialty = null, string? Phone = null, string? Email = null);
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

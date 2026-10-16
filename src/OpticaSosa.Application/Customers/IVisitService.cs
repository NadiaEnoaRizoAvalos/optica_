using System.ComponentModel.DataAnnotations;

namespace OpticaSosa.Application.Customers;

public sealed class VisitInput
{
    public Guid SubmissionId { get; set; } = Guid.NewGuid();
    public Guid CustomerVersion { get; set; }
    [Required(ErrorMessage = "Ingresá el motivo administrativo."), StringLength(100, ErrorMessage = "El motivo admite hasta 100 caracteres.")]
    public string Reason { get; set; } = "";
    [StringLength(500, ErrorMessage = "Las observaciones admiten hasta 500 caracteres.")]
    public string? AdministrativeNotes { get; set; }
}

public interface IVisitService
{
    Task<CustomerResult> StartAsync(Guid customerId, VisitInput input, CancellationToken ct = default);
    Task<CustomerResult> CloseAsync(Guid customerId, Guid visitId, Guid version, bool completed, CancellationToken ct = default);
}

using OpticaSosa.Application.Commerce;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Technical;

namespace OpticaSosa.Application.Inventory;

public sealed record AssignTechnicalInput(Guid SubmissionId, Guid OrderVersion, Guid PublicationId, Guid PublicationVersion,
    TechnicalEyes Eyes, Guid? PreviousAssignmentId = null, string? ReplacementReason = null);
public sealed record TechnicalOrderLine(Guid OrderId, Guid OrderVersion, Guid CustomerId, Guid LineId, Guid VariantId,
    string ProductName, string VariantCode, ProductCategory Category, int Quantity, OpticaSosa.Domain.Commerce.FulfilmentSource Source);
public sealed record TechnicalOrderLinePage(IReadOnlyList<TechnicalOrderLine> Items, int Page, bool HasMore);
// Sólo los datos del trabajo vinculado, sin motivos clínicos, historial general o importes comerciales.
public sealed record OrderTechnicalProjection(Guid OrderId, Guid LineId, Guid AssignmentId, Guid PublicationId, Guid SourceRevisionId,
    int RevisionNumber, PublicationStatus Status, TechnicalEyes Eyes, TechnicalPurpose Purpose, string Product,
    string? Material, decimal? RefractiveIndex, string? Treatment, EyeValues Right, EyeValues Left, decimal? PupillaryDistance,
    MountingSpecification Mounting, ContactSelection? RightContact, ContactSelection? LeftContact);
public interface IOrderTechnicalService
{
    Task<TechnicalOrderLinePage> LinesForProfessionalAsync(Guid? customerId = null, int page = 1, CancellationToken ct = default);
    Task<SalesResult> AssignAsync(Guid orderId, Guid lineId, AssignTechnicalInput input, CancellationToken ct = default);
    Task<OrderTechnicalProjection?> ProjectionAsync(Guid orderId, Guid lineId, CancellationToken ct = default);
}

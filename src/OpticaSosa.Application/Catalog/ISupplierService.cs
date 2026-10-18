using System.ComponentModel.DataAnnotations;
using OpticaSosa.Domain.Catalog;

namespace OpticaSosa.Application.Catalog;

public sealed class SupplierInput
{
    public Guid SubmissionId { get; set; } = Guid.NewGuid();
    public Guid Version { get; set; }
    [Required, StringLength(160)] public string Name { get; set; } = "";
    public bool SuppliesProducts { get; set; } = true;
    public bool IsLaboratory { get; set; }
    [StringLength(40)] public string? Phone { get; set; }
    [StringLength(254)] public string? Email { get; set; }
    [StringLength(60)] public string? TaxId { get; set; }
    [StringLength(1000)] public string? Services { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public SupplierProfile ToProfile() => new(Name, SuppliesProducts, IsLaboratory, Phone, Email, TaxId, Services, Notes);
}

public sealed record SupplierSummary(Guid Id, string Name, bool SuppliesProducts, bool IsLaboratory, bool IsActive);
public sealed record SupplierDetails(SupplierSummary Supplier, string? Phone, string? Email, string? TaxId,
    string? Services, string? Notes, Guid Version);
public sealed record SupplierPage(IReadOnlyList<SupplierSummary> Items, int Page, bool HasMore);
public enum CatalogResultCode { Success, Invalid, Duplicate, NotFound, Conflict }
public sealed record CatalogResult(CatalogResultCode Code, Guid? Id = null, string? Error = null)
{
    public bool Succeeded => Code == CatalogResultCode.Success;
}

public interface ISupplierService
{
    Task<SupplierPage> SearchAsync(string? search, bool includeInactive = false, bool laboratoriesOnly = false,
        int page = 1, CancellationToken ct = default);
    Task<SupplierDetails?> GetAsync(Guid id, CancellationToken ct = default);
    Task<CatalogResult> CreateAsync(SupplierInput input, CancellationToken ct = default);
    Task<CatalogResult> UpdateAsync(Guid id, SupplierInput input, CancellationToken ct = default);
}

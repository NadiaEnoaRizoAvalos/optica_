using System.ComponentModel.DataAnnotations;
using OpticaSosa.Domain.Catalog;

namespace OpticaSosa.Application.Catalog;

public sealed class ProductInput
{
    public Guid SubmissionId { get; set; } = Guid.NewGuid();
    public Guid Version { get; set; }
    [Required, StringLength(160)] public string Name { get; set; } = "";
    public ProductCategory Category { get; set; }
    [StringLength(100)] public string? Brand { get; set; }
    [StringLength(100)] public string? Model { get; set; }
    [StringLength(2000)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public IReadOnlyList<Guid> SupplierIds { get; set; } = [];
    public ProductProfile ToProfile() => new(Name, Category, Brand, Model, Description);
}

public sealed class VariantInput
{
    public Guid SubmissionId { get; set; } = Guid.NewGuid();
    public Guid ProductVersion { get; set; }
    public Guid Version { get; set; }
    [Required, StringLength(80)] public string Code { get; set; } = "";
    public int? UnitsPerPackage { get; set; }
    public FrameSpecification? Frame { get; set; }
    public LensSpecification? Lens { get; set; }
    public ContactSpecification? Contact { get; set; }
    public decimal? SalePrice { get; set; }
    public string? Currency { get; set; }
    public bool IsActive { get; set; } = true;
    public VariantSpecification ToSpecification(ProductCategory category) => new(Code, category, UnitsPerPackage, Frame, Lens, Contact);
}

public sealed record ProductSummary(Guid Id, string Name, ProductCategory Category, string? Brand, string? Model, bool IsActive, Guid Version);
public sealed record ProductPage(IReadOnlyList<ProductSummary> Items, int Page, bool HasMore);
public sealed record ProductDetails(ProductSummary Product, string? Description, IReadOnlyList<SupplierSummary> Suppliers);
public sealed record TechnicalVariant(Guid Id, Guid ProductId, VariantSpecification Specification, bool IsActive, bool SpecificationLocked, Guid Version);
public sealed record VariantDetails(TechnicalVariant Technical, Money? SalePrice);
public sealed record TechnicalVariantPage(IReadOnlyList<TechnicalVariant> Items, int Page, bool HasMore);
public sealed record VariantPage(IReadOnlyList<VariantDetails> Items, int Page, bool HasMore);
public sealed record CommercialConfiguration(string Currency, Guid Version);

public interface ICatalogService
{
    Task<ProductPage> SearchAsync(string? search, ProductCategory? category = null, bool includeInactive = false, int page = 1, CancellationToken ct = default);
    Task<ProductDetails?> GetAsync(Guid id, CancellationToken ct = default);
    Task<CatalogResult> CreateAsync(ProductInput input, CancellationToken ct = default);
    Task<CatalogResult> UpdateAsync(Guid id, ProductInput input, CancellationToken ct = default);
    Task<VariantPage> VariantsAsync(Guid productId, bool includeInactive = false, int page = 1, CancellationToken ct = default);
    Task<VariantDetails?> VariantAsync(Guid productId, Guid id, CancellationToken ct = default);
    Task<CatalogResult> CreateVariantAsync(Guid productId, VariantInput input, CancellationToken ct = default);
    Task<CatalogResult> UpdateVariantAsync(Guid productId, Guid id, VariantInput input, CancellationToken ct = default);
    Task<CommercialConfiguration> ConfigurationAsync(CancellationToken ct = default);
    Task<CatalogResult> ConfigureCurrencyAsync(string currency, Guid version, CancellationToken ct = default);
}

public interface ITechnicalCatalog
{
    Task<ProductPage> SearchAsync(string? search, ProductCategory? category = null, int page = 1, CancellationToken ct = default);
    Task<TechnicalVariantPage> VariantsAsync(Guid productId, int page = 1, CancellationToken ct = default);
    Task<TechnicalVariant?> VariantAsync(Guid productId, Guid id, CancellationToken ct = default);
}

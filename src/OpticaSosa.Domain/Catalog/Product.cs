namespace OpticaSosa.Domain.Catalog;

public enum ProductCategory { Frame = 1, Lens = 2, ContactLens = 3, Accessory = 4 }
public enum ProductUnit { Each = 1, ClosedBox = 2 }

public sealed record ProductProfile
{
    public string Name { get; }
    public ProductCategory Category { get; }
    public string? Brand { get; }
    public string? Model { get; }
    public string? Description { get; }

    public ProductProfile(string name, ProductCategory category, string? brand = null, string? model = null, string? description = null)
    {
        if (!Enum.IsDefined(category)) throw new ArgumentException("Categoría de producto inválida.");
        Name = CatalogText.Required(name, 160, "el nombre del producto"); Category = category;
        Brand = CatalogText.Optional(brand, 100); Model = CatalogText.Optional(model, 100);
        Description = CatalogText.Optional(description, 2000);
    }
}

public sealed class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string NameKey { get; private set; } = "";
    public ProductCategory Category { get; private set; }
    public string? Brand { get; private set; }
    public string BrandKey { get; private set; } = "";
    public string? Model { get; private set; }
    public string ModelKey { get; private set; } = "";
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public Guid Version { get; private set; }
    public string CreatedByUserId { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }

    private Product() { }
    public Product(Guid id, ProductProfile profile, string actor, DateTime utcNow)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(actor) || utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Identificación o fecha inválida.");
        Id = id; Category = profile.Category; CreatedByUserId = actor; CreatedAtUtc = utcNow;
        Update(profile, true);
    }
    public void Update(ProductProfile profile, bool active)
    {
        if (profile.Category != Category) throw new ArgumentException("No se puede cambiar la categoría de un producto existente.");
        Name = profile.Name; NameKey = CatalogText.Key(Name);
        Brand = profile.Brand; BrandKey = CatalogText.Key(Brand ?? ""); Model = profile.Model; ModelKey = CatalogText.Key(Model ?? "");
        Description = profile.Description; IsActive = active; Touch();
    }
    public void Touch() => Version = Guid.NewGuid();
    public bool Matches(ProductProfile p) => Name == p.Name && Category == p.Category && Brand == p.Brand && Model == p.Model && Description == p.Description;
}

public sealed class ProductSupplier
{
    public Guid ProductId { get; private set; }
    public Guid SupplierId { get; private set; }
    public bool IsActive { get; private set; }
    private ProductSupplier() { }
    public ProductSupplier(Guid productId, Guid supplierId)
    {
        if (productId == Guid.Empty || supplierId == Guid.Empty) throw new ArgumentException("Referencia inválida.");
        ProductId = productId; SupplierId = supplierId; IsActive = true;
    }
    public void SetActive(bool active) => IsActive = active;
}

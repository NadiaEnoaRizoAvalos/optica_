using System.ComponentModel.DataAnnotations;

namespace OpticaSosa.Domain.Catalog;

public sealed record SupplierProfile
{
    public string Name { get; }
    public bool SuppliesProducts { get; }
    public bool IsLaboratory { get; }
    public string? Phone { get; }
    public string? Email { get; }
    public string? TaxId { get; }
    public string? Services { get; }
    public string? Notes { get; }
    public string NameKey => CatalogText.Key(Name);
    public string? TaxKey => TaxId is null ? null : CatalogText.Key(TaxId).Replace("-", "").Replace(" ", "");

    public SupplierProfile(string name, bool suppliesProducts, bool isLaboratory, string? phone = null,
        string? email = null, string? taxId = null, string? services = null, string? notes = null)
    {
        Name = CatalogText.Required(name, 160, "el nombre del proveedor o laboratorio");
        if (!suppliesProducts && !isLaboratory) throw new ArgumentException("Seleccioná proveedor, laboratorio o ambos.");
        SuppliesProducts = suppliesProducts;
        IsLaboratory = isLaboratory;
        Phone = CatalogText.Optional(phone, 40);
        Email = CatalogText.Optional(email, 254);
        TaxId = CatalogText.Optional(taxId, 60);
        Services = CatalogText.Optional(services, 1000);
        Notes = CatalogText.Optional(notes, 2000);
        if (Phone is not null && !new PhoneAttribute().IsValid(Phone)) throw new ArgumentException("El teléfono no es válido.");
        if (Email is not null && !new EmailAddressAttribute().IsValid(Email)) throw new ArgumentException("El correo no es válido.");
        if (TaxId is not null && (TaxKey!.Length == 0 || TaxKey.Any(c => !char.IsLetterOrDigit(c))))
            throw new ArgumentException("El identificador fiscal admite letras, números, espacios y guiones.");
    }
}

public sealed class Supplier
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string NameKey { get; private set; } = "";
    public bool SuppliesProducts { get; private set; }
    public bool IsLaboratory { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? TaxId { get; private set; }
    public string? TaxKey { get; private set; }
    public string? Services { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }
    public Guid Version { get; private set; }
    public string CreatedByUserId { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }

    private Supplier() { }

    public Supplier(Guid id, SupplierProfile profile, string actor, DateTime utcNow)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(actor) || utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Identificación o fecha de creación inválida.");
        Id = id; CreatedByUserId = actor; CreatedAtUtc = utcNow;
        Update(profile, true);
    }

    public void Update(SupplierProfile profile, bool isActive)
    {
        Name = profile.Name; NameKey = profile.NameKey;
        SuppliesProducts = profile.SuppliesProducts; IsLaboratory = profile.IsLaboratory;
        Phone = profile.Phone; Email = profile.Email; TaxId = profile.TaxId; TaxKey = profile.TaxKey;
        Services = profile.Services; Notes = profile.Notes;
        IsActive = isActive; Touch();
    }

    public void Touch() => Version = Guid.NewGuid();

    public bool Matches(SupplierProfile profile) => Name == profile.Name && SuppliesProducts == profile.SuppliesProducts &&
        IsLaboratory == profile.IsLaboratory && Phone == profile.Phone && Email == profile.Email && TaxId == profile.TaxId &&
        Services == profile.Services && Notes == profile.Notes;
}

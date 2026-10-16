using System.Text;

namespace OpticaSosa.Domain.Clinical;

public sealed record ProfessionalProfile
{
    public string FullName { get; }
    public string? Registration { get; }
    public string? Jurisdiction { get; }
    public string? Specialty { get; }
    public string? Phone { get; }
    public string? Email { get; }
    public string DuplicateKey => string.Join('|', FullName, Registration ?? "", Jurisdiction ?? "").ToUpperInvariant();

    public ProfessionalProfile(string fullName, string? registration, string? jurisdiction, string? specialty = null, string? phone = null, string? email = null)
    {
        FullName = Clean(fullName, 120) ?? throw new ArgumentException("El nombre del profesional es obligatorio.");
        Registration = Clean(registration, 60);
        Jurisdiction = Clean(jurisdiction, 80);
        Specialty = Clean(specialty, 100); Phone = Clean(phone, 40); Email = Clean(email, 254);
        if (Email is not null && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(Email)) throw new ArgumentException("El correo no es válido.");
        if (Phone is not null && !new System.ComponentModel.DataAnnotations.PhoneAttribute().IsValid(Phone)) throw new ArgumentException("El teléfono no es válido.");
    }

    private static string? Clean(string? value, int limit)
    {
        var clean = string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Normalize(NormalizationForm.FormC);
        if (clean.Length > limit || clean.Any(c => char.IsControl(c) || c == '|'))
            throw new ArgumentException($"El campo admite hasta {limit} caracteres y no admite caracteres de control ni |.");
        return clean.Length == 0 ? null : clean;
    }
}

public sealed class Professional
{
    public Guid Id { get; private set; }
    public string FullName { get; private set; } = "";
    public string? Registration { get; private set; }
    public string? Jurisdiction { get; private set; }
    public string? Specialty { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string DuplicateKey { get; private set; } = "";
    public bool IsActive { get; private set; } = true;
    public Guid Version { get; private set; }
    public string CreatedByUserId { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }

    private Professional() { }

    public Professional(Guid id, ProfessionalProfile profile, string actor, DateTime utcNow)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Identificación inválida.");
        Id = id;
        CreatedByUserId = actor;
        CreatedAtUtc = utcNow;
        Update(profile, true);
    }

    public void Update(ProfessionalProfile profile, bool isActive)
    {
        FullName = profile.FullName;
        Registration = profile.Registration;
        Jurisdiction = profile.Jurisdiction;
        Specialty = profile.Specialty; Phone = profile.Phone; Email = profile.Email;
        DuplicateKey = profile.DuplicateKey;
        IsActive = isActive;
        Touch();
    }
    public void Touch() => Version = Guid.NewGuid();
}

using System.ComponentModel.DataAnnotations;
using OpticaSosa.Domain.Business;

namespace OpticaSosa.Web.Pages.Admin;

public sealed class BusinessInput
{
    public Guid Version { get; set; }
    [StringLength(120, ErrorMessage = "El nombre admite hasta 120 caracteres.")] public string? TradeName { get; set; }
    [StringLength(200, ErrorMessage = "La dirección admite hasta 200 caracteres.")] public string? Address { get; set; }
    [StringLength(80, ErrorMessage = "La provincia admite hasta 80 caracteres.")] public string? Province { get; set; }
    [StringLength(100, ErrorMessage = "La localidad admite hasta 100 caracteres.")] public string? Locality { get; set; }
    [StringLength(40, ErrorMessage = "El teléfono admite hasta 40 caracteres."), Phone(ErrorMessage = "El teléfono no es válido.")] public string? Phone { get; set; }
    [StringLength(40, ErrorMessage = "El WhatsApp admite hasta 40 caracteres."), Phone(ErrorMessage = "El WhatsApp no es válido.")] public string? WhatsApp { get; set; }
    [StringLength(254, ErrorMessage = "El correo admite hasta 254 caracteres."), EmailAddress(ErrorMessage = "El correo no es válido.")] public string? Email { get; set; }
    [StringLength(20, ErrorMessage = "El CUIT admite hasta 20 caracteres con separadores.")] public string? Cuit { get; set; }
    [StringLength(2000, ErrorMessage = "Los datos adicionales admiten hasta 2000 caracteres.")] public string? AdditionalDetails { get; set; }
    public bool RemoveLogo { get; set; }

    public BusinessProfile ToProfile() => new(TradeName, Address, Province, Locality, Phone, WhatsApp, Email, Cuit, AdditionalDetails);
    public static BusinessInput From(BusinessProfile p, Guid version) => new()
    {
        Version = version, TradeName = p.TradeName, Address = p.Address, Province = p.Province, Locality = p.Locality,
        Phone = p.Phone, WhatsApp = p.WhatsApp, Email = p.Email, Cuit = p.Cuit, AdditionalDetails = p.AdditionalDetails
    };
}

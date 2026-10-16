using System.ComponentModel.DataAnnotations;
using OpticaSosa.Domain.Customers;

namespace OpticaSosa.Application.Customers;

public sealed class CustomerInput
{
    public Guid SubmissionId { get; set; } = Guid.NewGuid();
    public Guid Version { get; set; }
    [Required(ErrorMessage = "Ingresá el nombre."), StringLength(80, ErrorMessage = "El nombre admite hasta 80 caracteres.")]
    public string FirstName { get; set; } = "";
    [Required(ErrorMessage = "Ingresá el apellido."), StringLength(80, ErrorMessage = "El apellido admite hasta 80 caracteres.")]
    public string LastName { get; set; } = "";
    [Required(ErrorMessage = "Ingresá el documento."), StringLength(32, ErrorMessage = "El documento admite hasta 32 caracteres.")]
    public string Document { get; set; } = "";
    [Phone(ErrorMessage = "El teléfono no es válido."), StringLength(40, ErrorMessage = "El teléfono admite hasta 40 caracteres.")]
    public string? Phone { get; set; }
    [EmailAddress(ErrorMessage = "El correo no es válido."), StringLength(254, ErrorMessage = "El correo admite hasta 254 caracteres.")]
    public string? Email { get; set; }
    public bool ConfirmPossibleDuplicate { get; set; }

    public CustomerData ToData() => new(FirstName, LastName, Document, Phone, Email);
}

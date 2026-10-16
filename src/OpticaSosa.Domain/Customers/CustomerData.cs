using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;

namespace OpticaSosa.Domain.Customers;

public sealed record CustomerData
{
    public string FirstName { get; }
    public string LastName { get; }
    public string Document { get; }
    public string? Phone { get; }
    public string? Email { get; }

    public CustomerData(string firstName, string lastName, string document, string? phone, string? email)
    {
        FirstName = Required(firstName, 80, "nombre");
        LastName = Required(lastName, 80, "apellido");
        Document = Required(document, 32, "documento");
        if (Document.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && c is not '.' and not '-') ||
            DocumentKey(Document).Length == 0)
            throw new ArgumentException("El documento sólo admite letras, números, espacios, puntos y guiones.");
        Phone = Optional(phone, 40, "teléfono");
        Email = Optional(email, 254, "correo");
        if (Phone is not null && (!new PhoneAttribute().IsValid(Phone) || PhoneKey(Phone).Length == 0))
            throw new ArgumentException("El teléfono no es válido.");
        if (Email is not null && !new EmailAddressAttribute().IsValid(Email))
            throw new ArgumentException("El correo no es válido.");
    }

    public static string Fold(string value) => string.Concat(value.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        .Normalize(NormalizationForm.FormC).ToUpperInvariant();

    public static string DocumentKey(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    public static string PhoneKey(string value) => string.Concat(value.Where(char.IsDigit));

    public static string Required(string? value, int maxLength, string label)
    {
        var trimmed = string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
            throw new ArgumentException($"El campo {label} es obligatorio y admite hasta {maxLength} caracteres.");
        return trimmed;
    }

    public static string? Optional(string? value, int maxLength, string label) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, maxLength, label);
}

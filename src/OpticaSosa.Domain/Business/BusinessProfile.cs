using System.ComponentModel.DataAnnotations;

namespace OpticaSosa.Domain.Business;

public sealed record BusinessProfile(string? TradeName, string? Address, string? Province, string? Locality,
    string? Phone, string? WhatsApp, string? Email, string? Cuit, string? AdditionalDetails)
{
    public BusinessProfile ValidateAndNormalize()
    {
        string? Text(string? value, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var text = value.Trim();
            if (text.Length > maximum) throw new ArgumentException($"Hay un campo que supera el máximo permitido de {maximum} caracteres.");
            return text;
        }
        var result = new BusinessProfile(Text(TradeName, 120), Text(Address, 200), Text(Province, 80), Text(Locality, 100),
            Text(Phone, 40), Text(WhatsApp, 40), Text(Email, 254), Text(Cuit, 20), Text(AdditionalDetails, 2000));
        if ((result.Phone is not null && !new PhoneAttribute().IsValid(result.Phone)) ||
            (result.WhatsApp is not null && !new PhoneAttribute().IsValid(result.WhatsApp)))
            throw new ArgumentException("Revisá los números de teléfono y WhatsApp.");
        if (result.Email is not null && !new EmailAddressAttribute().IsValid(result.Email))
            throw new ArgumentException("El correo no es válido.");
        if (result.Cuit is not null)
        {
            if (result.Cuit.Any(c => !char.IsAsciiDigit(c) && c is not '-' and not ' ') ||
                result.Cuit.Count(char.IsAsciiDigit) != 11)
                throw new ArgumentException("El CUIT debe contener 11 dígitos; podés usar guiones o espacios.");
            result = result with { Cuit = string.Concat(result.Cuit.Where(char.IsAsciiDigit)) };
        }
        return result;
    }
}

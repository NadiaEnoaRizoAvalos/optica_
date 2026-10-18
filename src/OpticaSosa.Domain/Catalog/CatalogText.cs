using System.Text;

namespace OpticaSosa.Domain.Catalog;

public static class CatalogText
{
    public static string Required(string? value, int limit, string label) =>
        Optional(value, limit) ?? throw new ArgumentException($"Indicá {label}.");

    public static string? Optional(string? value, int limit)
    {
        var normalized = (value ?? "").Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length > limit || normalized.Any(char.IsControl))
            throw new ArgumentException($"El campo admite hasta {limit} caracteres sin caracteres de control.");
        normalized = string.Join(' ', normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length == 0 ? null : normalized;
    }

    public static string Key(string value) => value.ToUpperInvariant();
}

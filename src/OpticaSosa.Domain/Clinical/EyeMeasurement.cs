namespace OpticaSosa.Domain.Clinical;

public enum EyeSide { Right, Left }

public sealed record EyeValues(decimal? Sphere = null, decimal? Cylinder = null, int? Axis = null,
    decimal? Addition = null, decimal? Prism = null, string? Base = null);

public sealed class EyeMeasurement
{
    public Guid RevisionId { get; private set; }
    public EyeSide Side { get; private set; }
    public int? SphereMilli { get; private set; }
    public int? CylinderMilli { get; private set; }
    public int? Axis { get; private set; }
    public int? AdditionMilli { get; private set; }
    public int? PrismMilli { get; private set; }
    public string? Base { get; private set; }

    private EyeMeasurement() { }
    public EyeMeasurement(Guid revisionId, EyeSide side, EyeValues values)
    {
        if (revisionId == Guid.Empty || !Enum.IsDefined(side)) throw new ArgumentException("Lateralidad inválida.");
        RevisionId = revisionId;
        Side = side;
        SphereMilli = Scale(values.Sphere, 1000, "esfera");
        CylinderMilli = Scale(values.Cylinder, 1000, "cilindro");
        AdditionMilli = Scale(values.Addition, 1000, "adición");
        PrismMilli = Scale(values.Prism, 1000, "prisma");
        if (values.Axis is < 0 or > 180) throw new ArgumentException("El eje debe ser un entero entre 0 y 180 grados.");
        Axis = values.Axis;
        Base = ClinicalText.Optional(values.Base, 60, "base");
    }

    public EyeValues ToValues() => new(SphereMilli / 1000m, CylinderMilli / 1000m, Axis, AdditionMilli / 1000m, PrismMilli / 1000m, Base);

    public static int? Scale(decimal? value, int factor, string label)
    {
        if (value is null) return null;
        if (value < -9999m || value > 9999m || decimal.Truncate(value.Value * factor) != value.Value * factor)
            throw new ArgumentException($"Revisá {label}: excede la precisión o capacidad del campo (hasta 9999 en valor absoluto).");
        return decimal.ToInt32(value.Value * factor);
    }
}

internal static class ClinicalText
{
    internal static string Required(string? value, int max, string label) => Optional(value, max, label)
        ?? throw new ArgumentException($"El campo {label} es obligatorio.");
    internal static string? Optional(string? value, int max, string label)
    {
        var clean = value?.Trim();
        if (string.IsNullOrWhiteSpace(clean)) return null;
        if (clean.Length > max || clean.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t'))
            throw new ArgumentException($"Revisá {label}: admite hasta {max} caracteres.");
        return clean;
    }
}

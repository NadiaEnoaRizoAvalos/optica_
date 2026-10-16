namespace OpticaSosa.Domain.Backups;

public static class BackupFolder
{
    public const string Default = "Respaldos";
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 240) throw new ArgumentException("Indicá una carpeta relativa de hasta 240 caracteres, sin letra de unidad.");
        var normalized = value.Replace('\\', '/');
        foreach (var part in normalized.Split('/'))
        {
            if (string.IsNullOrWhiteSpace(part) || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
                part.Any(c => char.IsControl(c) || "<>:\"|?*".Contains(c)))
                throw new ArgumentException("Usá una carpeta relativa válida, por ejemplo Optica/Respaldos; no rutas absolutas ni enlaces.");
            var stem = part.Split('.')[0].ToUpperInvariant();
            if (new[] { "CON", "PRN", "AUX", "NUL", "CLOCK$" }.Contains(stem) ||
                (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && char.IsDigit(stem[3])))
                throw new ArgumentException("La carpeta contiene un nombre reservado de Windows.");
        }
        return normalized;
    }
}

using System.Text.Json;

namespace OpticaSosa.Infrastructure.Backups;

// Vinculación de instalación/carpeta; no es cifrado ni una certificación anticlones.
public static class BackupTargetBinding
{
    private sealed record Marker(int Format, string VolumeId, Guid InstallationId, Guid BindingId);

    public static Guid Bind(string directory, string volumeId, Guid installationId)
    {
        var path = MarkerPath(directory, installationId);
        if (File.Exists(path)) return Read(path, volumeId, installationId).BindingId;
        var marker = new Marker(1, volumeId, installationId, Guid.NewGuid());
        // No se sobrescribe un marcador previo ni se cambia el ACL de la carpeta.
        var temporary = BackupFileSafety.Child(directory, $".optica-target-{Guid.NewGuid():N}.pending");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, marker);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        Validate(directory, volumeId, installationId, marker.BindingId);
        return marker.BindingId;
    }

    public static void Validate(string directory, string volumeId, Guid installationId, Guid? bindingId)
    {
        if (bindingId is null || bindingId == Guid.Empty) throw new IOException("La carpeta aún no fue vinculada con autorización.");
        var marker = Read(MarkerPath(directory, installationId), volumeId, installationId);
        if (marker.BindingId != bindingId) throw new IOException("El marcador del destino no coincide. No se escribe ni se elige otro disco.");
    }

    private static string MarkerPath(string directory, Guid installationId) =>
        BackupFileSafety.Child(directory, $".optica-backup-target-{installationId:N}.json");

    private static Marker Read(string path, string volumeId, Guid installationId)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 4096) throw new IOException("Falta un marcador válido en el destino configurado.");
            var marker = JsonSerializer.Deserialize<Marker>(File.ReadAllText(path));
            if (marker is null || marker.Format != 1 || marker.VolumeId != volumeId || marker.InstallationId != installationId || marker.BindingId == Guid.Empty)
                throw new IOException("El destino no pertenece a la vinculación esperada.");
            return marker;
        }
        catch (JsonException ex) { throw new IOException("Marcador de destino dañado. No se modifica.", ex); }
    }
}

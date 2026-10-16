using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpticaSosa.Application.Backups;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Backups;

public sealed class BackupFolderService(ServerBackupFolders folders, ICurrentUser current,
    IOptions<BackupRuntimeOptions> options, IDbContextFactory<OpticaDbContext> factory) : IBackupFolderService
{
    private string Demand()
    {
        if (current.Id is null || !current.HasPermission(Permissions.ManageBackups)) throw new UnauthorizedAccessException();
        return current.Id;
    }
    public Task<BackupFolderPage> BrowseAsync(string volumeId, string relativeFolder = "", int page = 1, CancellationToken ct = default)
    {
        Demand(); ct.ThrowIfCancellationRequested();
        return Task.FromResult(folders.Browse(volumeId, relativeFolder, page, options.Value.FolderCreationAuthorized, ct));
    }
    public Task<BackupFolderSelection> SelectAsync(string volumeId, string relativeFolder, CancellationToken ct = default)
    { Demand(); ct.ThrowIfCancellationRequested(); return Task.FromResult(folders.Select(volumeId, relativeFolder)); }

    public async Task<BackupAction> CreateAsync(string volumeId, string parent, string name, bool confirmed, CancellationToken ct = default)
    {
        var actor = Demand();
        if (!options.Value.FolderCreationAuthorized) return new(false, "Crear carpetas no está autorizado en esta instalación. Podés seleccionar una existente.");
        if (!confirmed) return new(false, "Confirmá explícitamente la creación de una carpeta nueva.");
        // Registrar intención antes de crear: si falla la auditoría no se toca el disco.
        await using var db = await factory.CreateDbContextAsync(ct);
        var operation = Guid.NewGuid().ToString();
        db.AuditEntries.Add(new AuditEntry { ActorId = actor, Action = "backup.folder.create.requested", TargetId = operation });
        await db.SaveChangesAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested(); folders.Create(volumeId, parent, name);
            db.AuditEntries.Add(new AuditEntry { ActorId = actor, Action = "backup.folder.created", TargetId = operation });
            await db.SaveChangesAsync(CancellationToken.None);
            return new(true, "Carpeta creada. No se activaron respaldos ni se vinculó el destino. Seleccionala para continuar.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            db.AuditEntries.Add(new AuditEntry { ActorId = actor, Action = "backup.folder.create.failed", TargetId = operation });
            await db.SaveChangesAsync(CancellationToken.None);
            return new(false, "No se completó la creación. Revisá nombre, conexión, espacio y permisos privados. No se sobrescribió ni eliminó ningún elemento.");
        }
    }
}

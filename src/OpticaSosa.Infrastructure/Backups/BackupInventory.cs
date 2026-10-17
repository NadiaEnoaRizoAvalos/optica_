using Microsoft.Data.Sqlite;

namespace OpticaSosa.Infrastructure.Backups;

public static class BackupInventory
{
    // Sólo metadatos y conteos; nunca exporta valores clínicos, contraseñas o hashes de cuentas.
    public static async Task<SortedDictionary<string, long>> ReadAsync(string directory)
    {
        var path = BackupFileSafety.Child(directory, "optica.db");
        await using var db = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await db.OpenAsync();
        var tables = new List<string>();
        await using (var list = db.CreateCommand())
        {
            list.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
            await using var rows = await list.ExecuteReaderAsync();
            while (await rows.ReadAsync()) tables.Add(rows.GetString(0));
        }
        var result = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            await using var count = db.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM \"" + table.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
            result[table] = (long)(await count.ExecuteScalarAsync())!;
        }
        return result;
    }
}

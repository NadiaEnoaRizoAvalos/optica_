using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Infrastructure;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task IndependentContextsPersistCommittedDataAndRollBackUncommittedData()
    {
        // Directorio exclusivo: nunca se abre una base existente ni se usan datos personales.
        var root = Path.Combine(Path.GetTempPath(), "OpticaSosa.Tests", Guid.NewGuid().ToString("N"));
        var paths = new StoragePaths(Path.Combine(root, "private"), Path.Combine(root, "wwwroot"));
        try
        {
            using var provider = new ServiceCollection().AddPersistence(paths).BuildServiceProvider();
            var factory = provider.GetRequiredService<IDbContextFactory<OpticaDbContext>>();
            await using (var first = await factory.CreateDbContextAsync())
            {
                await first.Database.ExecuteSqlRawAsync("CREATE TABLE Probe (Id INTEGER PRIMARY KEY)");
                await using (var transaction = await first.Database.BeginTransactionAsync())
                {
                    await first.Database.ExecuteSqlRawAsync("INSERT INTO Probe (Id) VALUES (1)");
                    await transaction.CommitAsync();
                }
                await using (var transaction = await first.Database.BeginTransactionAsync())
                {
                    await first.Database.ExecuteSqlRawAsync("INSERT INTO Probe (Id) VALUES (2)");
                    await transaction.RollbackAsync();
                }
            }

            await using var second = await factory.CreateDbContextAsync();
            Assert.Equal(1L, await second.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM Probe").SingleAsync());
            await second.Database.OpenConnectionAsync();
            await using var command = second.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA foreign_keys";
            Assert.Equal(1L, await command.ExecuteScalarAsync());
            Assert.True(File.Exists(paths.DatabaseFile));
            Assert.True(Directory.Exists(paths.AttachmentsDirectory));

            await using var third = await factory.CreateDbContextAsync();
            Assert.NotSame(second, third);
            Assert.NotSame(second.Database.GetDbConnection(), third.Database.GetDbConnection());
        }
        finally
        {
            // Sólo el directorio GUID creado por esta prueba, tras cerrar sus conexiones.
            var target = new DirectoryInfo(Path.GetFullPath(root));
            var expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OpticaSosa.Tests"));
            if (!string.Equals(target.Parent?.FullName, expectedParent, StringComparison.OrdinalIgnoreCase) ||
                !Guid.TryParseExact(target.Name, "N", out _))
                throw new InvalidOperationException("Se rechazó limpiar una ruta ajena a la prueba.");

            if (target.Exists) target.Delete(recursive: true);
        }
    }
}

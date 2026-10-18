using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OpticaSosa.Infrastructure.Persistence.Migrations;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class StockMigrationTests
{
    [Fact]
    public void StockMigrationOnlyCreatesItsOwnTablesAndIndexes()
    {
        var migration = new StockReservationsAndTechnicalAssignments();
        Assert.All(migration.UpOperations, operation => Assert.True(operation is CreateTableOperation or CreateIndexOperation));
        Assert.Equal(new[] { "OrderTechnicalAssignments", "StockBatches", "StockMovements", "StockReservations" },
            migration.UpOperations.OfType<CreateTableOperation>().Select(x => x.Name).Order().ToArray());
    }
}

using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OpticaSosa.Infrastructure.Persistence.Migrations;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class LaboratoryMigrationTests
{
    [Fact]
    public void LaboratoryMigrationOnlyCreatesItsThreeNewTablesAndIndexes()
    {
        var migration = new LaboratoryOrders();
        Assert.All(migration.UpOperations, operation => Assert.True(operation is CreateTableOperation or CreateIndexOperation));
        Assert.Equal(new[] { "LaboratoryChanges", "LaboratoryOrders", "LaboratoryReceipts" },
            migration.UpOperations.OfType<CreateTableOperation>().Select(x => x.Name).Order().ToArray());
    }
}

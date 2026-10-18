using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OpticaSosa.Infrastructure.Persistence.Migrations;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class SalesMigrationTests
{
    [Fact]
    public void SalesMigrationCreatesOnlyItsFourNewTablesAndIndexes()
    {
        var migration = new SalesOrdersAndPayments();
        Assert.All(migration.UpOperations, operation => Assert.True(operation is CreateTableOperation or CreateIndexOperation));
        Assert.Equal(new[] { "OrderCommandReceipts", "OrderMoneyEntries", "SalesLines", "SalesOrders" },
            migration.UpOperations.OfType<CreateTableOperation>().Select(x => x.Name).Order().ToArray());
    }
}

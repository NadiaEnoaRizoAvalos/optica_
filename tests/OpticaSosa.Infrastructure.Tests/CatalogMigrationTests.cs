using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OpticaSosa.Infrastructure.Persistence.Migrations;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class CatalogMigrationTests
{
    [Fact]
    public void CatalogMigrationOnlyCreatesNewTablesIndexesAndItsOwnCurrencyConfiguration()
    {
        var migration = new ProductCatalog();
        var tables = migration.UpOperations.OfType<CreateTableOperation>().Select(x => x.Name).Order().ToArray();
        Assert.Equal(new[] { "CommercialSettings", "ProductSuppliers", "ProductVariants", "Products" }.Order(), tables);
        Assert.All(migration.UpOperations, operation => Assert.True(operation is CreateTableOperation or CreateIndexOperation or InsertDataOperation));
        Assert.Equal("CommercialSettings", Assert.Single(migration.UpOperations.OfType<InsertDataOperation>()).Table);
    }
}

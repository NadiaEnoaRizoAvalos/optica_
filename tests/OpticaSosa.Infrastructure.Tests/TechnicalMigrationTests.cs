using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OpticaSosa.Infrastructure.Persistence.Migrations;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class TechnicalMigrationTests
{
    [Fact]
    public void PublicationMigrationOnlyCreatesItsOwnTableAndIndexes()
    {
        var migration = new TechnicalPublications();
        Assert.All(migration.UpOperations, operation => Assert.True(operation is CreateTableOperation or CreateIndexOperation));
        Assert.Equal("TechnicalPublications", Assert.Single(migration.UpOperations.OfType<CreateTableOperation>()).Name);
    }
}

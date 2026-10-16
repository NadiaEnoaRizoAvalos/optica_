using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Web.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OpticaDbContext>
{
    public OpticaDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<OpticaDbContext>().UseSqlite("Data Source=design-time.db").Options);
}

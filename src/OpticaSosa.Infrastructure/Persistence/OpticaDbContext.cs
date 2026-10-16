using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Domain.Customers;
using OpticaSosa.Domain.Business;
using OpticaSosa.Domain.Clinical;

namespace OpticaSosa.Infrastructure.Persistence;

public sealed class OpticaDbContext(DbContextOptions<OpticaDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<BusinessSettings> BusinessSettings => Set<BusinessSettings>();
    public DbSet<Professional> Professionals => Set<Professional>();
    public DbSet<ClinicalRecord> ClinicalRecords => Set<ClinicalRecord>();
    public DbSet<ClinicalRevision> ClinicalRevisions => Set<ClinicalRevision>();
    public DbSet<EyeMeasurement> EyeMeasurements => Set<EyeMeasurement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpticaDbContext).Assembly);
    }
}

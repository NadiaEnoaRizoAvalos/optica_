using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Domain.Customers;
using OpticaSosa.Domain.Business;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Work;

namespace OpticaSosa.Infrastructure.Persistence;

public sealed class OpticaDbContext(DbContextOptions<OpticaDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<OpticaSosa.Domain.Backups.BackupSettings> BackupSettings => Set<OpticaSosa.Domain.Backups.BackupSettings>();
    public DbSet<OpticaSosa.Domain.Backups.BackupRun> BackupRuns => Set<OpticaSosa.Domain.Backups.BackupRun>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<BusinessSettings> BusinessSettings => Set<BusinessSettings>();
    public DbSet<Professional> Professionals => Set<Professional>();
    public DbSet<ClinicalRecord> ClinicalRecords => Set<ClinicalRecord>();
    public DbSet<ClinicalRevision> ClinicalRevisions => Set<ClinicalRevision>();
    public DbSet<EyeMeasurement> EyeMeasurements => Set<EyeMeasurement>();
    public DbSet<ClinicalAttachment> ClinicalAttachments => Set<ClinicalAttachment>();
    public DbSet<ClinicalFilePolicy> ClinicalFilePolicies => Set<ClinicalFilePolicy>();
    public DbSet<OpticalWork> OpticalWorks => Set<OpticalWork>();
    public DbSet<WorkTransition> WorkTransitions => Set<WorkTransition>();
    public DbSet<QualityControl> QualityControls => Set<QualityControl>();
    public DbSet<QualityTemplate> QualityTemplates => Set<QualityTemplate>();
    public DbSet<OpticaSosa.Domain.Catalog.Supplier> Suppliers => Set<OpticaSosa.Domain.Catalog.Supplier>();
    public DbSet<OpticaSosa.Domain.Catalog.Product> Products => Set<OpticaSosa.Domain.Catalog.Product>();
    public DbSet<OpticaSosa.Domain.Catalog.ProductVariant> ProductVariants => Set<OpticaSosa.Domain.Catalog.ProductVariant>();
    public DbSet<OpticaSosa.Domain.Catalog.ProductSupplier> ProductSuppliers => Set<OpticaSosa.Domain.Catalog.ProductSupplier>();
    public DbSet<OpticaSosa.Domain.Catalog.CommercialSettings> CommercialSettings => Set<OpticaSosa.Domain.Catalog.CommercialSettings>();
    public DbSet<OpticaSosa.Domain.Technical.TechnicalPublication> TechnicalPublications => Set<OpticaSosa.Domain.Technical.TechnicalPublication>();
    public DbSet<OpticaSosa.Domain.Commerce.SalesOrder> SalesOrders => Set<OpticaSosa.Domain.Commerce.SalesOrder>();
    public DbSet<OpticaSosa.Domain.Commerce.SalesLine> SalesLines => Set<OpticaSosa.Domain.Commerce.SalesLine>();
    public DbSet<OpticaSosa.Domain.Commerce.OrderMoneyEntry> OrderMoneyEntries => Set<OpticaSosa.Domain.Commerce.OrderMoneyEntry>();
    public DbSet<OpticaSosa.Domain.Commerce.OrderCommandReceipt> OrderCommandReceipts => Set<OpticaSosa.Domain.Commerce.OrderCommandReceipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpticaDbContext).Assembly);
    }
}

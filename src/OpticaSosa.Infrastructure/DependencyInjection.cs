using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Application.Customers;
using OpticaSosa.Infrastructure.Customers;
using OpticaSosa.Application.Business;
using OpticaSosa.Infrastructure.Business;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Infrastructure.Clinical;
using OpticaSosa.Infrastructure.Delivery;

namespace OpticaSosa.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, StoragePaths paths)
    {
        Directory.CreateDirectory(paths.DataDirectory);
        Directory.CreateDirectory(paths.AttachmentsDirectory);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabaseFile,
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = 5
        }.ToString();

        services.AddSingleton(paths);
        services.AddOptions<ClinicalFileOptions>().Validate(x => x.Valid(), "Configuración de archivos clínicos inválida.");
        services.AddSingleton<ClinicalFileGate>();
        services.AddSingleton<IStorageCapacity, StorageCapacity>();
        services.AddDbContextFactory<OpticaDbContext>(options => options.UseSqlite(connectionString));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<OpticaDbContext>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ISecurityAudit, SecurityAudit>();
        services.AddScoped<LocalProvisioning>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IVisitService, VisitService>();
        services.AddScoped<IBusinessSettingsService, BusinessSettingsService>();
        services.AddScoped<IProfessionalService, ProfessionalService>();
        services.AddScoped<OpticaSosa.Application.Catalog.ISupplierService, OpticaSosa.Infrastructure.Catalog.SupplierService>();
        services.AddScoped<OpticaSosa.Application.Catalog.ICatalogService, OpticaSosa.Infrastructure.Catalog.CatalogService>();
        services.AddScoped<OpticaSosa.Application.Catalog.ITechnicalCatalog, OpticaSosa.Infrastructure.Catalog.TechnicalCatalog>();
        services.AddScoped<OpticaSosa.Application.Technical.ITechnicalPublicationService, OpticaSosa.Infrastructure.Technical.TechnicalPublicationService>();
        services.AddScoped<OpticaSosa.Application.Commerce.ISalesOrderService, OpticaSosa.Infrastructure.Commerce.SalesOrderService>();
        services.AddScoped<OpticaSosa.Application.Commerce.IOrderPaymentService, OpticaSosa.Infrastructure.Commerce.OrderPaymentService>();
        services.AddScoped<OpticaSosa.Application.Inventory.IStockService, OpticaSosa.Infrastructure.Inventory.StockService>();
        services.AddScoped<OpticaSosa.Application.Inventory.IOrderTechnicalService, OpticaSosa.Infrastructure.Inventory.OrderTechnicalService>();
        services.AddScoped<OpticaSosa.Application.Laboratory.ILaboratoryService, OpticaSosa.Infrastructure.Laboratory.LaboratoryService>();
        services.AddScoped<OpticaSosa.Application.Inventory.IInventoryAdministration, OpticaSosa.Infrastructure.Inventory.InventoryAdministration>();
        services.AddScoped<IClinicalService, ClinicalService>();
        services.AddScoped<IClinicalDirectory, ClinicalDirectory>();
        services.AddScoped<IPrivateAttachmentStore, PrivateAttachmentStore>();
        services.AddScoped<IClinicalFilePolicyService, ClinicalFilePolicyService>();
        services.AddScoped<IClinicalAttachmentService, ClinicalAttachmentService>();
        services.AddScoped<ClinicalFileValidation>();
        services.AddScoped<IClinicalSnapshotService, ClinicalSnapshotService>();
        services.AddSingleton<ClinicalSnapshotWriter>();
        services.AddSingleton<OpticaSosa.Infrastructure.Backups.BackupExecutionGate>();
        services.AddSingleton<OpticaSosa.Infrastructure.Backups.IBackupTargets, OpticaSosa.Infrastructure.Backups.WindowsBackupTargets>();
        services.AddSingleton<OpticaSosa.Infrastructure.Backups.ServerBackupFolders>();
        services.AddSingleton<OpticaSosa.Infrastructure.Backups.IBackupSigningCertificate, OpticaSosa.Infrastructure.Backups.WindowsBackupSigningCertificate>();
        services.AddSingleton<OpticaSosa.Infrastructure.Backups.BackupAuthenticity>();
        services.AddSingleton<OpticaSosa.Infrastructure.Backups.IBackupProtectionProbe, OpticaSosa.Infrastructure.Backups.WindowsBackupProtectionProbe>();
        services.AddSingleton<OpticaSosa.Infrastructure.Backups.BackupProtectionGuard>();
        services.AddScoped<OpticaSosa.Application.Backups.IBackupProtectionService, OpticaSosa.Infrastructure.Backups.BackupProtectionService>();
        services.AddScoped<OpticaSosa.Application.Backups.IBackupFolderService, OpticaSosa.Infrastructure.Backups.BackupFolderService>();
        services.AddSingleton<OpticaSosa.Infrastructure.Backups.BackupVerifier>();
        services.AddSingleton<OpticaSosa.Infrastructure.Backups.BackupCoordinator>();
        services.AddScoped<OpticaSosa.Application.Backups.IBackupService, OpticaSosa.Infrastructure.Backups.BackupService>();
        services.AddScoped<OpticaSosa.Application.Work.IWorkService, OpticaSosa.Infrastructure.Work.WorkService>();
        services.AddDelivery();
        return services;
    }
}

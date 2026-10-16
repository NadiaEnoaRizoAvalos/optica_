using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Infrastructure.Clinical;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Web.Tests;

internal sealed class ClinicalScenario : IDisposable
{
    private readonly IServiceScope scope;
    public IProfessionalService Professionals { get; }
    public IClinicalService Clinical { get; }
    public OpticaDbContext Db => scope.ServiceProvider.GetRequiredService<OpticaDbContext>();
    public IDbContextFactory<OpticaDbContext> Factory => scope.ServiceProvider.GetRequiredService<IDbContextFactory<OpticaDbContext>>();
    public ICurrentUser Actor { get; }
    public IClinicalFilePolicyService Policy { get; }
    public IClinicalAttachmentService Attachments => AttachmentsWith();
    public IClinicalSnapshotService Snapshots => new ClinicalSnapshotService(Factory, Actor,
        scope.ServiceProvider.GetRequiredService<StoragePaths>(), scope.ServiceProvider.GetRequiredService<IStorageCapacity>(),
        scope.ServiceProvider.GetRequiredService<IOptions<ClinicalFileOptions>>(), scope.ServiceProvider.GetRequiredService<ClinicalFileGate>());
    public ClinicalAttachmentService AttachmentsWith(IPrivateAttachmentStore? store = null, IStorageCapacity? capacity = null) => new(Factory, Actor,
        store ?? scope.ServiceProvider.GetRequiredService<IPrivateAttachmentStore>(), Policy,
        scope.ServiceProvider.GetRequiredService<ClinicalFileValidation>(), capacity ?? scope.ServiceProvider.GetRequiredService<IStorageCapacity>(),
        scope.ServiceProvider.GetRequiredService<IOptions<ClinicalFileOptions>>(), scope.ServiceProvider.GetRequiredService<ClinicalFileGate>(), TimeProvider.System);

    private ClinicalScenario(IServiceScope scope, ICurrentUser actor)
    {
        this.scope = scope;
        Actor = actor;
        Professionals = new ProfessionalService(Factory, actor, TimeProvider.System);
        Clinical = new ClinicalService(Factory, actor, TimeProvider.System);
        Policy = new ClinicalFilePolicyService(Factory, actor, scope.ServiceProvider.GetRequiredService<IOptions<ClinicalFileOptions>>(), scope.ServiceProvider.GetRequiredService<ClinicalFileGate>());
    }
    public static async Task<ClinicalScenario> OpenAsync(SecurityApp app, string name = "profesional.ficticio")
    {
        var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByNameAsync(name))!;
        return new(scope, new TestActor(user.Id, (await users.GetRolesAsync(user)).ToArray()));
    }
    public static ClinicalInput Input(Guid professionalId) => new()
    {
        ProfessionalId = professionalId, Date = new(2026, 10, 15), Source = "Documento ficticio de prueba",
        Kind = ClinicalKind.MedicalPrescription, PupillaryDistance = 62.25m,
        Right = new() { Sphere = 1.125m, Cylinder = 0m, Axis = 180, Prism = .125m }, Left = new() { Sphere = -2.375m }
    };
    public async Task<Guid> AddProfessionalAsync()
    {
        var result = await Professionals.CreateAsync(new() { FullName = "Profesional ficticio", Registration = "MAT-FIC", Jurisdiction = "Ámbito ficticio" });
        if (!result.Succeeded) throw new InvalidOperationException("Fallo de preparación ficticia.");
        return result.Id!.Value;
    }
    public void Dispose() => scope.Dispose();
    private sealed record TestActor(string? Id, IReadOnlyList<string> Roles) : ICurrentUser
    {
        public bool HasPermission(string permission) => Roles.Any(x => Permissions.Grants(x, permission));
    }
}

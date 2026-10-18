using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Technical;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Technical;

namespace OpticaSosa.Web.Tests;

internal sealed class TechnicalTestContext : IAsyncDisposable
{
    public SecurityApp App { get; }
    public ClinicalScenario Clinical { get; }
    public TechnicalPublicationService Publications { get; }
    public Guid CustomerId { get; }
    public Guid RecordId { get; }
    public ClinicalInput Source { get; }
    private TechnicalTestContext(SecurityApp app, ClinicalScenario clinical, Guid customer, Guid record, ClinicalInput source)
    {
        App = app; Clinical = clinical; CustomerId = customer; RecordId = record; Source = source;
        Publications = new(clinical.Factory, clinical.Actor, TimeProvider.System);
    }
    public static async Task<TechnicalTestContext> CreateAsync()
    {
        var app = new SecurityApp(); await app.InitializeAsync();
        using var customers = await CustomerScenario.OpenAsync(app);
        var customer = (await customers.Customers.CreateAsync(CustomerScenario.Input())).CustomerId!.Value;
        var clinical = await ClinicalScenario.OpenAsync(app);
        var source = ClinicalScenario.Input(await clinical.AddProfessionalAsync());
        source.Right.Prism = 0;
        source.Notes = "NOTA_CLINICA_FICTICIA_NO_PUBLICABLE";
        source.ReportedSymptoms = "SINTOMA_FICTICIO_NO_PUBLICABLE";
        var record = await clinical.Clinical.CreateAsync(customer, source);
        if (!record.Succeeded) throw new InvalidOperationException("No se pudo preparar la receta ficticia.");
        return new(app, clinical, customer, record.Id!.Value, source);
    }
    public async Task<TechnicalPublicationInput> InputAsync()
    {
        var source = (await Clinical.Clinical.GetAsync(CustomerId, RecordId))!;
        return new() { Confirmed = true, Purpose = TechnicalPurpose.SpectacleLenses, IncludeRight = true,
            Product = "Cristal ficticio", SourceRevisionId = source.RevisionId, RecordVersion = source.Version,
            Mounting = new(rightHeightMm: 17.25m, rightMonocularDistanceMm: 31.12m) };
    }
    public async ValueTask DisposeAsync() { Clinical.Dispose(); await App.DisposeAsync(); }
}

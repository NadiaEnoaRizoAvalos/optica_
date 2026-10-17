using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Backups;
using OpticaSosa.Infrastructure.Clinical;
using OpticaSosa.Infrastructure.Persistence;
using OpticaSosa.Viewer;
using Xunit;

namespace OpticaSosa.Web.Tests;

internal sealed class ViewerFixture : IAsyncDisposable
{
    private readonly FictitiousViewerSigner signer = new();
    public SecurityApp Source { get; }
    public string Backup { get; private set; } = "";
    public string Workspace => Path.Combine(Source.Root, "viewer-work");
    public string Fingerprint => signer.Fingerprint;
    public Guid Customer { get; private set; }
    public Guid Record { get; private set; }
    public Guid Image { get; private set; }
    public Guid Pdf { get; private set; }
    public Guid Work { get; private set; }
    public ViewerApp Viewer { get; private set; } = null!;

    public ViewerFixture()
    {
        Source = new SecurityApp(preserveData: !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPTICA_VIEWER_EXPORT_ROOT")), configureServices: services =>
        {
            services.AddSingleton<IBackupSigningCertificate>(signer);
            services.Configure<BackupRuntimeOptions>(o =>
            {
                o.SigningEnabled = true; o.RequireSignature = true; o.WorkerEnabled = false;
                o.TrustedSigningCertificatesSha256 = [signer.Fingerprint];
            });
        });
    }

    public async Task InitializeAsync()
    {
        await Source.InitializeAsync();
        // Sólo el directorio temporal ficticio de esta prueba; nunca ACL globales/operativas.
        if (OperatingSystem.IsWindows()) StorageProtection.RestrictToCurrentWindowsUser(Source.Root);
        using var customers = await CustomerScenario.OpenAsync(Source);
        Customer = (await customers.Customers.CreateAsync(CustomerScenario.Input("VISOR-FICTICIO-001"))).CustomerId!.Value;
        using var pro = await ClinicalScenario.OpenAsync(Source);
        var input = ClinicalScenario.Input(await pro.AddProfessionalAsync());
        Record = (await pro.Clinical.CreateAsync(Customer, input)).Id!.Value;
        var details = (await pro.Clinical.GetAsync(Customer, Record))!;
        foreach (var (bytes, name, mime) in new[] { (BusinessForms.Logo(), "foto-ficticia.png", "image/png"), (AttachmentTestContext.Pdf(), "receta-ficticia.pdf", "application/pdf") })
        {
            using var content = new MemoryStream(bytes);
            var upload = await pro.Attachments.UploadAsync(Customer, Record, new(Guid.NewGuid(), details.RevisionId, details.Version, name, mime, bytes.Length, content));
            Assert.True(upload.Succeeded, upload.Error);
            if (mime == "image/png") Image = upload.Id!.Value; else Pdf = upload.Id!.Value;
            details = (await pro.Clinical.GetAsync(Customer, Record))!;
        }
        input.SubmissionId = Guid.NewGuid(); input.Version = details.Version; input.CorrectionReason = "Corrección ficticia conservada"; input.Right.Sphere = 1.375m;
        Assert.True((await pro.Clinical.CorrectAsync(Customer, Record, input)).Succeeded);
        var history = ClinicalScenario.Input(input.ProfessionalId); history.Kind = ClinicalKind.VisualHistory;
        history.Right = new(); history.Left = new(); history.PupillaryDistance = null;
        history.Notes = "Antecedente inventado para la prueba del visor";
        history.ReportedSymptoms = "Síntoma exclusivamente ficticio";
        var savedHistory = await pro.Clinical.CreateAsync(Customer, history);
        Assert.True(savedHistory.Succeeded, savedHistory.Error);
        var service = QualityWorkflowTests.Service(pro);
        Work = (await service.CreateAsync(new() { CustomerId = Customer, Product = "Trabajo ficticio del visor", Frame = "Armazón de prueba", PrescriptionId = Record })).Id!.Value;
        await QualityWorkflowTests.PrepareAsync(service, Work);
        Assert.True((await service.SaveControlAsync(Work, await QualityWorkflowTests.InputAsync(service, Work, QualityStage.Technical))).Succeeded);
        using var sales = await ClinicalScenario.OpenAsync(Source, "ventas.ficticio");
        var delivery = QualityWorkflowTests.Service(sales);
        Assert.True((await delivery.SaveControlAsync(Work, await QualityWorkflowTests.InputAsync(delivery, Work, QualityStage.Delivery))).Succeeded);
        using var accounts = await AccountScenario.OpenAsync(Source);
        for (var mask = 1; mask <= 7; mask++)
        {
            var roles = Roles.All.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
            Assert.True((await accounts.Accounts.CreateAsync($"mixta{mask}.ficticia", SecurityApp.TestPassword, roles)).Succeeded);
        }
        Backup = (await Source.Services.GetRequiredService<ClinicalSnapshotWriter>().CreateAsync(Path.Combine(Source.Root, "signed-backup"))).Directory;
        Viewer = new(Backup, Workspace, Fingerprint);
    }

    public async Task RefreshAsync()
    {
        await Viewer.DisposeAsync();
        Backup = (await Source.Services.GetRequiredService<ClinicalSnapshotWriter>().CreateAsync(Path.Combine(Source.Root, "signed-backup"))).Directory;
        Viewer = new(Backup, Workspace, Fingerprint);
    }

    public async ValueTask DisposeAsync()
    {
        if (Viewer is not null) await Viewer.DisposeAsync();
        await Source.DisposeAsync(); signer.Dispose();
    }

    internal static async Task<Dictionary<string, string>> HashesAsync(string directory)
    {
        var result = new Dictionary<string, string>();
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order())
        {
            await using var input = File.OpenRead(file);
            result.Add(Path.GetRelativePath(directory, file), Convert.ToHexString(await SHA256.HashDataAsync(input)));
        }
        return result;
    }

    private sealed class FictitiousViewerSigner : IBackupSigningCertificate, IDisposable
    {
        private readonly RSA key = RSA.Create(3072);
        private readonly X509Certificate2 certificate;
        public string Fingerprint => certificate.GetCertHashString(HashAlgorithmName.SHA256);
        public FictitiousViewerSigner()
        {
            var request = new CertificateRequest("CN=VISOR-FICTICIO-NO-OPERATIVO", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        }
        public X509Certificate2 Open() => new(certificate);
        public void Dispose() { certificate.Dispose(); key.Dispose(); }
    }
}

internal sealed class ViewerApp(string backup, string workspace, string fingerprint, bool fictitious = true, string? publicRoot = null) : WebApplicationFactory<ViewerEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (publicRoot is not null) builder.UseWebRoot(publicRoot);
        builder.UseSetting("Viewer:BackupDirectory", backup);
        builder.UseSetting("Viewer:Workspace", workspace);
        builder.UseSetting("Viewer:FictitiousDataConfirmed", fictitious.ToString());
        builder.UseSetting("Backups:TrustedSigningCertificatesSha256:0", fingerprint);
    }
    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
}

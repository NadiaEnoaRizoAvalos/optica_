using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Backups;
using OpticaSosa.Infrastructure.Clinical;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;
using SkiaSharp;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class PhysicalRecoveryFixtureTests
{
    [Fact]
    public async Task FictitiousRehearsalPreservesHistoryAccountsDocumentsAndBothQualityStages()
    {
        using var keys = new RehearsalCertificate();
        await using var test = new BackupTestContext(configureServices: services =>
        {
            services.AddSingleton<IBackupSigningCertificate>(keys);
            services.Configure<BackupRuntimeOptions>(o => { o.SigningEnabled = true; o.RequireSignature = true; o.TrustedSigningCertificatesSha256 = [keys.Fingerprint]; });
        });
        await test.InitializeAsync(); test.Clock.Now = DateTimeOffset.UtcNow;
        using (var browser = test.App.Browser())
        {
            await BrowserForms.LoginAsync(browser, "admin.ficticio");
            BusinessForms.Saved(await BusinessForms.SaveAsync(browser, new() { ["Input.TradeName"] = "OPTICA FICTICIA - SIMULACRO" }));
        }
        using var customers = await CustomerScenario.OpenAsync(test.App);
        var customer = (await customers.Customers.CreateAsync(CustomerScenario.Input("SIMULACRO-001"))).CustomerId!.Value;
        Assert.True((await customers.Customers.CreateAsync(CustomerScenario.Input("SIMULACRO-002"))).Succeeded);
        using var pro = await ClinicalScenario.OpenAsync(test.App);
        var professional = await pro.AddProfessionalAsync();
        var input = ClinicalScenario.Input(professional);
        input.Right.Cylinder = -.750m; input.Right.Addition = 1.250m; input.Right.Base = "Interna ficticia";
        input.Left.Cylinder = -.625m; input.Left.Axis = 45; input.Left.Addition = 1.500m; input.Left.Prism = .250m;
        var medical = await pro.Clinical.CreateAsync(customer, input); Assert.True(medical.Succeeded);
        input.SubmissionId = Guid.NewGuid(); input.Version = (await pro.Clinical.GetAsync(customer, medical.Id!.Value))!.Version;
        input.CorrectionReason = "Corrección exclusivamente ficticia para el simulacro"; input.Right.Sphere = 1.375m;
        Assert.True((await pro.Clinical.CorrectAsync(customer, medical.Id.Value, input)).Succeeded);
        foreach (var kind in new[] { ClinicalKind.OpticalPrescription, ClinicalKind.VisualHistory })
        {
            var additional = ClinicalScenario.Input(professional); additional.Kind = kind;
            if (kind == ClinicalKind.VisualHistory)
            {
                additional.Right = new(); additional.Left = new(); additional.PupillaryDistance = null;
                additional.Notes = "Consulta y control ficticios, independientes de la compra"; additional.IsFollowUp = true;
            }
            additional.ReportedSymptoms = "Información inventada para recuperación; sin diagnóstico automático";
            Assert.True((await pro.Clinical.CreateAsync(customer, additional)).Succeeded);
        }
        var png = Photograph(); var pdf = Document();
        foreach (var (bytes, name, mime) in new[] { (png, "receta-ficticia.png", "image/png"), (pdf, "receta-ficticia.pdf", "application/pdf") })
        {
            var detail = (await pro.Clinical.GetAsync(customer, medical.Id.Value))!; using var stream = new MemoryStream(bytes);
            Assert.True((await pro.Attachments.UploadAsync(customer, medical.Id.Value,
                new(Guid.NewGuid(), detail.RevisionId, detail.Version, name, mime, bytes.Length, stream))).Succeeded);
        }
        using var sales = await ClinicalScenario.OpenAsync(test.App, "ventas.ficticio");
        var work = QualityWorkflowTests.Service(pro); var delivery = QualityWorkflowTests.Service(sales);
        var created = await work.CreateAsync(new() { CustomerId = customer, Product = "Anteojos de simulacro", Frame = "Armazón ficticio" });
        Assert.True(created.Succeeded); await QualityWorkflowTests.PrepareAsync(work, created.Id!.Value);
        Assert.True((await work.SaveControlAsync(created.Id.Value, await QualityWorkflowTests.InputAsync(work, created.Id.Value, QualityStage.Technical))).Succeeded);
        Assert.True((await delivery.SaveControlAsync(created.Id.Value, await QualityWorkflowTests.InputAsync(delivery, created.Id.Value, QualityStage.Delivery))).Succeeded);
        var snapshot = await test.App.Services.GetRequiredService<ClinicalSnapshotWriter>().CreateAsync(Path.Combine(test.App.Root, "snapshot-simulacro"));
        var restored = await test.App.Services.GetRequiredService<BackupVerifier>().RestoreIsolatedAsync(snapshot.Directory);
        Assert.Equal(await BackupInventory.ReadAsync(snapshot.Directory), await BackupInventory.ReadAsync(restored));
        var options = new DbContextOptionsBuilder<OpticaDbContext>().UseSqlite($"Data Source={Path.Combine(restored, "optica.db")};Mode=ReadOnly;Pooling=False").Options;
        await using var db = new OpticaDbContext(options);
        Assert.Equal(2, await db.Customers.CountAsync()); Assert.Equal(4, await db.ClinicalRevisions.CountAsync());
        Assert.Equal(2, await db.QualityControls.CountAsync()); Assert.Equal(3, await db.Users.CountAsync());
        var hasher = new PasswordHasher<ApplicationUser>();
        foreach (var user in await db.Users.ToListAsync())
            Assert.NotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, user.PasswordHash!, SecurityApp.TestPassword));
        Assert.Equal(3, await db.UserRoles.CountAsync());
        Assert.Equal(1.125m, (await pro.Clinical.GetAsync(customer, medical.Id.Value, 1))!.Data.Right.Sphere);
        Assert.Equal(1.375m, (await pro.Clinical.GetAsync(customer, medical.Id.Value))!.Data.Right.Sphere);
        var export = Environment.GetEnvironmentVariable("OPTICA_REHEARSAL_EXPORT_ROOT");
        if (!string.IsNullOrWhiteSpace(export)) await ExportAsync(export, snapshot.Directory, keys);
    }

    private static async Task ExportAsync(string export, string snapshot, RehearsalCertificate keys)
    {
        var project = new DirectoryInfo(AppContext.BaseDirectory);
        while (project is not null && !File.Exists(Path.Combine(project.FullName, "OpticaSosa.sln"))) project = project.Parent;
        Assert.NotNull(project);
        var root = Path.GetFullPath(export); var folder = new DirectoryInfo(root);
        Assert.Equal(Path.Combine(project.FullName, "data"), folder.Parent?.FullName, ignoreCase: true);
        Assert.StartsWith("prueba-recuperacion-52a-", folder.Name);
        BackupFileSafety.RejectLinks(root); Assert.True(folder.Exists); Assert.False(Directory.Exists(Path.Combine(root, "backup")));
        foreach (var file in Directory.GetFiles(snapshot, "*", SearchOption.AllDirectories))
        {
            var target = BackupFileSafety.Child(Path.Combine(root, "backup"), Path.GetRelativePath(snapshot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target, overwrite: false);
        }
        var counts = await BackupInventory.ReadAsync(snapshot);
        await File.WriteAllTextAsync(Path.Combine(root, "resultado-esperado.json"), JsonSerializer.Serialize(new
        { Fictitious = true, Tables = counts, DatabaseSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(snapshot, "optica.db")))),
            AccountsAndBothRolesStagesChecked = true, MedicalHistoryVersions = 2, RealPatientData = false }, new JsonSerializerOptions { WriteIndented = true }));
        await File.WriteAllTextAsync(Path.Combine(root, "firmante-publico.txt"), keys.Fingerprint);
        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            hashes[Path.GetRelativePath(root, file).Replace('\\', '/')] = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(file)));
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new { Format = 1, Purpose = "SIMULACRO FICTICIO 5.2.A", Files = hashes });
        await File.WriteAllBytesAsync(Path.Combine(root, "kit-manifest.json"), manifest);
        using var certificate = keys.Open(); var cms = new SignedCms(new ContentInfo(manifest), detached: true);
        cms.ComputeSignature(new CmsSigner(certificate) { DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1"), IncludeOption = X509IncludeOption.EndCertOnly }, silent: true);
        await File.WriteAllBytesAsync(Path.Combine(root, "kit-manifest.p7s"), cms.Encode());
    }

    private static byte[] Photograph()
    {
        using var bitmap = new SKBitmap(900, 300); using var canvas = new SKCanvas(bitmap); canvas.Clear(SKColors.White);
        using var font = new SKFont(SKTypeface.Default, 25); using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        canvas.DrawText("RECETA FICTICIA - NO UTILIZAR PARA ATENCION", 20, 55, SKTextAlign.Left, font, paint);
        canvas.DrawText("OD +1.375 / -0.750 x 180 - OI -2.375 / -0.625 x 45", 20, 115, SKTextAlign.Left, font, paint);
        canvas.DrawText("Simulacro de recuperacion. DP 62.25 mm.", 20, 175, SKTextAlign.Left, font, paint);
        using var image = SKImage.FromBitmap(bitmap); using var bytes = image.Encode(SKEncodedImageFormat.Png, 100); return bytes.ToArray();
    }
    private static byte[] Document()
    {
        using var output = new MemoryStream(); var offsets = new List<long> { 0 };
        void Write(string text) => output.Write(Encoding.ASCII.GetBytes(text));
        void Obj(int id, string value) { offsets.Add(output.Position); Write($"{id} 0 obj\n{value}\nendobj\n"); }
        Write("%PDF-1.4\n"); Obj(1, "<< /Type /Catalog /Pages 2 0 R >>"); Obj(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Obj(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>");
        Obj(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        var content = "BT /F1 14 Tf 40 760 Td (RECETA FICTICIA - SIMULACRO) Tj 0 -30 Td (OD +1.375 / -0.750 x180; OI -2.375 / -0.625 x45) Tj ET";
        Obj(5, $"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
        var xref = output.Position; Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Write($"{offset:D10} 00000 n \n");
        Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"); return output.ToArray();
    }
    private sealed class RehearsalCertificate : IBackupSigningCertificate, IDisposable
    {
        private readonly RSA rsa = RSA.Create(3072); private readonly X509Certificate2 certificate;
        public string Fingerprint => certificate.GetCertHashString(HashAlgorithmName.SHA256);
        public RehearsalCertificate()
        {
            var request = new CertificateRequest("CN=SIMULACRO-FICTICIO-NO-OPERATIVO", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddMonths(6));
        }
        public X509Certificate2 Open() => new(certificate);
        public void Dispose() { certificate.Dispose(); rsa.Dispose(); }
    }
}

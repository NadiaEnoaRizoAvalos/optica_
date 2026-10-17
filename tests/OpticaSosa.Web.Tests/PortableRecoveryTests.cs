using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Infrastructure.Backups;
using OpticaSosa.Infrastructure.Persistence;
using OpticaSosa.Domain.Work;
using OpticaSosa.Recovery;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class PortableRecoveryTests
{
    [Fact]
    public void TwoProtectedCustodiesRecoverSigningKeyAfterOriginalIsDisposed()
    {
        byte[] first, second; string fingerprint;
        using (var keys = new FictitiousRecoveryCertificate())
        {
            fingerprint = keys.Fingerprint;
            using var certificate = keys.Open();
            var pbe = new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 200000);
            first = certificate.ExportPkcs12(pbe, "Custodia-ficticia-A-93!");
            second = certificate.ExportPkcs12(pbe, "Custodia-ficticia-B-71!");
        }
        Assert.Equal(fingerprint, RecoveryKeyCheck.Verify(first, "Custodia-ficticia-A-93!".AsSpan(), fingerprint));
        Assert.Equal(fingerprint, RecoveryKeyCheck.Verify(second, "Custodia-ficticia-B-71!".AsSpan(), fingerprint));
        Assert.Throws<CryptographicException>(() => RecoveryKeyCheck.Verify(first, "contraseña-ficticia-incorrecta".AsSpan(), fingerprint));
        Assert.Throws<InvalidDataException>(() => RecoveryKeyCheck.Verify(second, "Custodia-ficticia-B-71!".AsSpan(), new string('0', 64)));
        Assert.NotEqual(first, second);
        Array.Clear(first); Array.Clear(second);
    }

    [Theory]
    [InlineData("empty-password")] [InlineData("corrupt-pfx")] [InlineData("public-only")]
    public void KeyCheckRejectsIncompleteRecoveryMaterial(string problem)
    {
        using var keys = new FictitiousRecoveryCertificate(); using var certificate = keys.Open();
        if (problem == "empty-password")
            Assert.Throws<InvalidDataException>(() => RecoveryKeyCheck.Verify([1], ReadOnlySpan<char>.Empty, keys.Fingerprint));
        if (problem == "corrupt-pfx")
            Assert.Throws<CryptographicException>(() => RecoveryKeyCheck.Verify([1, 2, 3], "ficticia".AsSpan(), keys.Fingerprint));
        if (problem == "public-only")
        {
            using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.RawData);
            var bytes = publicOnly.ExportPkcs12(new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 200000), "ficticia");
            Assert.Throws<InvalidDataException>(() => RecoveryKeyCheck.Verify(bytes, "ficticia".AsSpan(), keys.Fingerprint));
        }
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void PlaintextOrUnauthenticatedPrivateKeyIsNotAProtectedCustody(bool withMac)
    {
        using var keys = new FictitiousRecoveryCertificate(); using var certificate = keys.Open();
        using var rsa = certificate.GetRSAPrivateKey()!;
        var contents = new System.Security.Cryptography.Pkcs.Pkcs12SafeContents();
        contents.AddCertificate(certificate); contents.AddKeyUnencrypted(rsa);
        var builder = new System.Security.Cryptography.Pkcs.Pkcs12Builder();
        builder.AddSafeContentsUnencrypted(contents);
        if (withMac) builder.SealWithMac("ficticia", HashAlgorithmName.SHA256, 200000);
        else builder.SealWithoutIntegrity();
        var bytes = builder.Encode();
        try
        {
            if (withMac) Assert.Throws<InvalidDataException>(() => RecoveryKeyCheck.Verify(bytes, "ficticia".AsSpan(), keys.Fingerprint));
            else Assert.Throws<CryptographicException>(() => RecoveryKeyCheck.Verify(bytes, "ficticia".AsSpan(), keys.Fingerprint));
        }
        finally { Array.Clear(bytes); }
    }

    [Fact]
    public async Task OfflineConsoleRestoresAllExistingModulesWithoutHostOrOriginalKeys()
    {
        using var keys = new FictitiousRecoveryCertificate();
        await using var test = new BackupTestContext(configureServices: services =>
        {
            services.AddSingleton<IBackupSigningCertificate>(keys);
            services.Configure<BackupRuntimeOptions>(o => { o.SigningEnabled = true; o.RequireSignature = true; o.TrustedSigningCertificatesSha256 = [keys.Fingerprint]; });
        });
        await test.InitializeAsync();
        using var pro = await ClinicalScenario.OpenAsync(test.App); using var customers = await CustomerScenario.OpenAsync(test.App);
        var customer = (await customers.Customers.CreateAsync(CustomerScenario.Input())).CustomerId!.Value;
        var record = await pro.Clinical.CreateAsync(customer, ClinicalScenario.Input(await pro.AddProfessionalAsync()));
        foreach (var (bytes, name, mime) in new[] { (BusinessForms.Logo(), "ficticia.png", "image/png"), (AttachmentTestContext.Pdf(), "ficticia.pdf", "application/pdf") })
        {
            var detail = (await pro.Clinical.GetAsync(customer, record.Id!.Value))!; using var stream = new MemoryStream(bytes);
            Assert.True((await pro.Attachments.UploadAsync(customer, record.Id.Value, new(Guid.NewGuid(), detail.RevisionId, detail.Version, name, mime, bytes.Length, stream))).Succeeded);
        }
        var work = QualityWorkflowTests.Service(pro);
        var created = await work.CreateAsync(new() { CustomerId = customer, Product = "Producto ficticio", Frame = "Armazón ficticio" });
        await QualityWorkflowTests.PrepareAsync(work, created.Id!.Value);
        Assert.True((await work.SaveControlAsync(created.Id.Value, await QualityWorkflowTests.InputAsync(work, created.Id.Value, QualityStage.Technical))).Succeeded);
        await test.ConfigureAsync(); Assert.Equal("Complete", (await test.CopyAsync()).Status);
        var source = Assert.Single(Directory.GetDirectories(Assert.Single(Directory.GetDirectories(test.Targets.Root))));
        var before = await HashTree(source);
        var trusted = keys.Fingerprint; // Canal independiente del respaldo.
        var workspace = Path.Combine(test.App.Root, "otra-pc-ficticia"); Directory.CreateDirectory(workspace);
        Assert.Equal(0, await RecoveryConsole.RunAsync(["verify", "--backup", source, "--trust", keys.Fingerprint, "--workspace", workspace]));
        Assert.Empty(Directory.GetFileSystemEntries(workspace));
        keys.Dispose(); // La recuperación no usa la clave privada original ni el host web.
        Assert.Equal(0, await RecoveryConsole.RunAsync(["restore", "--backup", source, "--trust", trusted, "--workspace", workspace]));
        var restored = Assert.Single(Directory.GetDirectories(Path.Combine(workspace, "restore-tests")));
        Assert.Equal(before, await HashTree(source));
        Assert.Equal(before, await HashTree(restored));
        var options = new DbContextOptionsBuilder<OpticaDbContext>().UseSqlite($"Data Source={Path.Combine(restored, "optica.db")};Mode=ReadOnly;Pooling=False").Options;
        await using var db = new OpticaDbContext(options);
        Assert.Equal(3, await db.Users.CountAsync()); Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Equal(1, await db.Professionals.CountAsync()); Assert.Equal(1, await db.ClinicalRevisions.CountAsync());
        Assert.Equal(2, await db.ClinicalAttachments.CountAsync()); Assert.Equal(1, await db.QualityControls.CountAsync());
        Assert.False((await db.BackupSettings.SingleAsync()).Enabled);
        Assert.True(File.Exists(Path.Combine(restored, "configuration.json")));
    }

    [Theory]
    [InlineData("unknown-signer")] [InlineData("nonempty-workspace")] [InlineData("workspace-is-source")]
    public async Task ConsoleRefusesUnsafeRestoreWithoutChangingExistingMaterial(string problem)
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync(); await test.CopyAsync();
        var source = Assert.Single(Directory.GetDirectories(Assert.Single(Directory.GetDirectories(test.Targets.Root))));
        var workspace = Path.Combine(test.App.Root, "recuperacion-ficticia"); Directory.CreateDirectory(workspace);
        if (problem == "nonempty-workspace") await File.WriteAllTextAsync(Path.Combine(workspace, "conservar.txt"), "Documento ficticio existente");
        if (problem == "workspace-is-source") workspace = source;
        var before = await File.ReadAllBytesAsync(Path.Combine(source, "complete.json"));
        Assert.Equal(1, await RecoveryConsole.RunAsync(["restore", "--backup", source, "--trust", new string('0', 64), "--workspace", workspace]));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(source, "complete.json")));
        Assert.False(Directory.Exists(Path.Combine(workspace, "restore-tests")));
    }

    private static async Task<string> HashTree(string root)
    {
        var results = new List<string>();
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            results.Add(Path.GetRelativePath(root, file) + ":" + Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(file))));
        return string.Join("\n", results);
    }
    private sealed class FictitiousRecoveryCertificate : IBackupSigningCertificate, IDisposable
    {
        private readonly RSA rsa = RSA.Create(3072);
        private readonly X509Certificate2 certificate;
        public string Fingerprint => certificate.GetCertHashString(HashAlgorithmName.SHA256);
        public FictitiousRecoveryCertificate()
        {
            var request = new CertificateRequest("CN=Recuperacion ficticia", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            certificate = request.CreateSelfSigned(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        }
        public X509Certificate2 Open() => new(certificate);
        public void Dispose() { certificate.Dispose(); rsa.Dispose(); }
    }
}

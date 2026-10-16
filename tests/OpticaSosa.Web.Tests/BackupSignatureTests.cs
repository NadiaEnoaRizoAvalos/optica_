using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpticaSosa.Infrastructure.Backups;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class BackupSignatureTests
{
    [Fact]
    public async Task SignedSnapshotRestoresWithPublicCertificateAndNeverExportsPrivateKey()
    {
        using var keys = new FictitiousKeys();
        await using var test = SignedContext(keys); await test.InitializeAsync();
        using var professional = await ClinicalScenario.OpenAsync(test.App);
        using var customers = await CustomerScenario.OpenAsync(test.App);
        var customer = (await customers.Customers.CreateAsync(CustomerScenario.Input())).CustomerId!.Value;
        var record = await professional.Clinical.CreateAsync(customer, ClinicalScenario.Input(await professional.AddProfessionalAsync()));
        var detail = (await professional.Clinical.GetAsync(customer, record.Id!.Value))!;
        var image = BusinessForms.Logo(); using var upload = new MemoryStream(image);
        Assert.True((await professional.Attachments.UploadAsync(customer, record.Id.Value,
            new(Guid.NewGuid(), detail.RevisionId, detail.Version, "receta-ficticia.png", "image/png", image.Length, upload))).Succeeded);
        Assert.True((await test.ConfigureAsync()).Succeeded);
        Assert.Equal("Complete", (await test.CopyAsync()).Status);
        var source = CopyDirectory(test);
        var verifier = test.App.Services.GetRequiredService<BackupVerifier>();
        var restored = await verifier.RestoreIsolatedAsync(source);
        Assert.NotEqual(source, restored);
        Assert.Equal(image, await File.ReadAllBytesAsync(Assert.Single(Directory.GetFiles(Path.Combine(restored, "attachments", "clinical")))));
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(source, "optica.db")),
            await File.ReadAllBytesAsync(Path.Combine(restored, "optica.db")));
        var manifest = await File.ReadAllBytesAsync(Path.Combine(restored, "complete.json"));
        Assert.Equal(3, JsonNode.Parse(manifest)!["Format"]!.GetValue<int>());
        var cms = new SignedCms(new ContentInfo(manifest), detached: true);
        cms.Decode(await File.ReadAllBytesAsync(Path.Combine(restored, BackupAuthenticity.SignatureFile)));
        Assert.False(Assert.Single(cms.Certificates.Cast<X509Certificate2>()).HasPrivateKey);
        Assert.DoesNotContain(Directory.GetFiles(restored, "*", SearchOption.AllDirectories), p =>
            Path.GetExtension(p) is ".pfx" or ".p12" or ".key");
        var overview = await test.Coordinator.OverviewAsync(default);
        Assert.True(overview.SignaturesRequired); Assert.True(overview.SigningEnabled);
    }

    [Theory]
    [InlineData("manifest")] [InlineData("payload-and-hash")] [InlineData("missing-signature")]
    [InlineData("corrupt-signature")] [InlineData("downgrade")]
    public async Task TamperingNeverRemainsAnAcceptedSuccessfulCopy(string attack)
    {
        using var keys = new FictitiousKeys();
        await using var test = SignedContext(keys); await test.InitializeAsync(); await test.ConfigureAsync();
        var run = await test.CopyAsync(); var directory = CopyDirectory(test);
        var path = Path.Combine(directory, "complete.json");
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        if (attack == "manifest") manifest["CreatedAtUtc"] = "2026-10-19T12:00:00Z";
        if (attack == "payload-and-hash")
        {
            var forged = Encoding.UTF8.GetBytes("Contenido ficticio alterado por un atacante");
            await File.WriteAllBytesAsync(Path.Combine(directory, "recovery.txt"), forged);
            manifest["Files"]!["recovery.txt"] = Convert.ToHexString(SHA256.HashData(forged));
        }
        if (attack is "missing-signature" or "downgrade") File.Delete(Path.Combine(directory, BackupAuthenticity.SignatureFile));
        if (attack == "downgrade") manifest["Format"] = 2;
        if (attack == "corrupt-signature") await File.WriteAllTextAsync(Path.Combine(directory, BackupAuthenticity.SignatureFile), "Firma ficticia dañada");
        await File.WriteAllTextAsync(path, manifest.ToJsonString());
        Assert.False((await test.Coordinator.VerifyAsync(run.Id, "admin.ficticio", default)).Succeeded);
        var overview = await test.Coordinator.OverviewAsync(default);
        Assert.Null(overview.LastValidUtc); Assert.Equal("Corrupt", Assert.Single(overview.Runs).Status);
    }

    [Theory]
    [InlineData("unknown")] [InlineData("revoked")] [InlineData("expired")] [InlineData("no-key")]
    public async Task UnusableSignerFailsWithoutLosingPreviousRecoverableCopy(string reason)
    {
        using var keys = new FictitiousKeys();
        await using var test = SignedContext(keys); await test.InitializeAsync(); await test.ConfigureAsync();
        var good = await test.CopyAsync(); var directory = CopyDirectory(test);
        var before = await File.ReadAllBytesAsync(Path.Combine(directory, "complete.json"));
        var runtime = test.App.Services.GetRequiredService<IOptions<BackupRuntimeOptions>>().Value;
        if (reason == "unknown") runtime.TrustedSigningCertificatesSha256 = [];
        if (reason == "revoked") runtime.RevokedSigningCertificatesSha256 = [keys.Fingerprint];
        if (reason == "expired") test.Clock.Now = test.Clock.Now.AddYears(2);
        if (reason == "no-key") keys.PublicOnly = true;
        Assert.Equal("Failed", (await test.CopyAsync()).Status);
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(directory, "complete.json")));
        Assert.Equal("Complete", (await test.Coordinator.OverviewAsync(default)).Runs.Single(r => r.Id == good.Id).Status);
        runtime.TrustedSigningCertificatesSha256 = [keys.Fingerprint]; runtime.RevokedSigningCertificatesSha256 = [];
        await test.App.Services.GetRequiredService<BackupVerifier>().VerifyAsync(directory);
    }

    [Fact]
    public async Task CertificateEmbeddedInSignatureDoesNotEstablishTrustAndRevocationIsEnforced()
    {
        using var keys = new FictitiousKeys();
        await using var test = SignedContext(keys); await test.InitializeAsync(); await test.ConfigureAsync(); await test.CopyAsync();
        var directory = CopyDirectory(test); var verifier = test.App.Services.GetRequiredService<BackupVerifier>();
        var runtime = test.App.Services.GetRequiredService<IOptions<BackupRuntimeOptions>>().Value;
        runtime.TrustedSigningCertificatesSha256 = [];
        await Assert.ThrowsAsync<InvalidDataException>(() => verifier.VerifyAsync(directory));
        runtime.TrustedSigningCertificatesSha256 = [keys.Fingerprint]; runtime.RevokedSigningCertificatesSha256 = [keys.Fingerprint];
        await Assert.ThrowsAsync<InvalidDataException>(() => verifier.RestoreIsolatedAsync(directory));
        runtime.RevokedSigningCertificatesSha256 = [];
        await verifier.VerifyAsync(directory);
    }

    [Fact]
    public async Task LegacyCopiesRemainReadableOnlyUnderExplicitCompatibilityPolicy()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync(); await test.CopyAsync();
        var directory = CopyDirectory(test); var verifier = test.App.Services.GetRequiredService<BackupVerifier>();
        await verifier.VerifyAsync(directory);
        test.App.Services.GetRequiredService<IOptions<BackupRuntimeOptions>>().Value.RequireSignature = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => verifier.VerifyAsync(directory));
        Assert.Equal("Failed", (await test.CopyAsync()).Status);
        Assert.Single(Directory.GetDirectories(Path.GetDirectoryName(directory)!));
    }

    [Fact]
    public async Task SignedFormatCannotBeAcceptedWithoutSignatureEvenInLegacyMode()
    {
        using var keys = new FictitiousKeys();
        await using var test = SignedContext(keys); await test.InitializeAsync(); await test.ConfigureAsync(); await test.CopyAsync();
        var directory = CopyDirectory(test);
        test.App.Services.GetRequiredService<IOptions<BackupRuntimeOptions>>().Value.RequireSignature = false;
        File.Delete(Path.Combine(directory, BackupAuthenticity.SignatureFile));
        await Assert.ThrowsAsync<InvalidDataException>(() => test.App.Services.GetRequiredService<BackupVerifier>().VerifyAsync(directory));
    }

    [Theory]
    [InlineData(2048, true)] [InlineData(3072, false)]
    public async Task WeakOrUnauthorizedKeyUsageCannotProduceAcceptedCopies(int bits, bool digitalSignature)
    {
        using var keys = new FictitiousKeys(bits, digitalSignature);
        await using var test = SignedContext(keys); await test.InitializeAsync(); await test.ConfigureAsync();
        Assert.Equal("Failed", (await test.CopyAsync()).Status);
        Assert.Null((await test.Coordinator.OverviewAsync(default)).LastValidUtc);
    }

    private static string CopyDirectory(BackupTestContext test) => Assert.Single(
        Directory.GetDirectories(Assert.Single(Directory.GetDirectories(test.Targets.Root))));

    private static BackupTestContext SignedContext(FictitiousKeys keys) => new(configureServices: services =>
    {
        services.AddSingleton<IBackupSigningCertificate>(keys);
        services.Configure<BackupRuntimeOptions>(o =>
        { o.SigningEnabled = true; o.RequireSignature = true; o.TrustedSigningCertificatesSha256 = [keys.Fingerprint]; });
    });

    private sealed class FictitiousKeys : IBackupSigningCertificate, IDisposable
    {
        private readonly RSA rsa;
        private readonly X509Certificate2 certificate;
        public bool PublicOnly { get; set; }
        public string Fingerprint => certificate.GetCertHashString(HashAlgorithmName.SHA256);
        public FictitiousKeys(int bits = 3072, bool digitalSignature = true)
        {
            rsa = RSA.Create(bits);
            var request = new CertificateRequest("CN=Optica prueba ficticia", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(digitalSignature ? X509KeyUsageFlags.DigitalSignature : X509KeyUsageFlags.KeyEncipherment, true));
            var now = new BackupClock().Now;
            certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(1));
        }
        public X509Certificate2 Open() => PublicOnly
            ? X509CertificateLoader.LoadCertificate(certificate.RawData) : new X509Certificate2(certificate);
        public void Dispose() { certificate.Dispose(); rsa.Dispose(); }
    }
}

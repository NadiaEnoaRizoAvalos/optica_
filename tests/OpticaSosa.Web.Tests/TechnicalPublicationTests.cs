using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Technical;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Technical;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class TechnicalPublicationTests
{
    [Fact]
    public async Task PublicationCopiesOnlyAuthorizedEyesAndFieldsWithoutRequiringAPurchase()
    {
        await using var test = await TechnicalTestContext.CreateAsync(); var input = await test.InputAsync();
        var result = await test.Publications.PublishAsync(test.CustomerId, test.RecordId, input);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(result, await test.Publications.PublishAsync(test.CustomerId, test.RecordId, input));
        var publication = (await test.Publications.GetAsync(test.CustomerId, result.Id!.Value))!;
        Assert.Equal(PublicationStatus.Current, publication.Status);
        Assert.Equal(1.125m, publication.Right.Sphere); Assert.Equal(0m, publication.Right.Cylinder);
        Assert.Null(publication.Left.Sphere); Assert.Equal(62.25m, publication.PupillaryDistance);
        Assert.Equal(31.12m, publication.Request.Mounting.RightMonocularDistanceMm);
        Assert.Null(publication.Request.Mounting.LeftMonocularDistanceMm);
        var json = JsonSerializer.Serialize(publication);
        Assert.DoesNotContain("NOTA_CLINICA_FICTICIA_NO_PUBLICABLE", json);
        Assert.DoesNotContain("SINTOMA_FICTICIO_NO_PUBLICABLE", json);
        Assert.DoesNotContain("price", json, StringComparison.OrdinalIgnoreCase);
        await using var db = await test.Clinical.Factory.CreateDbContextAsync();
        Assert.Single(await db.ClinicalRecords.ToListAsync()); Assert.Single(await db.ClinicalRevisions.ToListAsync());
        Assert.Empty(await db.OpticalWorks.ToListAsync());
        var audit = Assert.Single(await db.AuditEntries.Where(x => x.Action == "technical.publication.created").ToListAsync());
        Assert.Equal(test.Clinical.Actor.Id, audit.ActorId); Assert.Equal(result.Id.ToString(), audit.TargetId);
    }
    [Theory]
    [InlineData("admin.ficticio")]
    [InlineData("ventas.ficticio")]
    public async Task AdministrationAndSalesCannotPublishRevokeOrBrowseClinicalPublications(string user)
    {
        await using var test = await TechnicalTestContext.CreateAsync(); using var actor = await ClinicalScenario.OpenAsync(test.App, user);
        var service = new TechnicalPublicationService(actor.Factory, actor.Actor, TimeProvider.System);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListAsync(test.CustomerId, test.RecordId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(test.CustomerId, Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await service.PublishAsync(test.CustomerId, test.RecordId, await test.InputAsync()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RevokeAsync(test.CustomerId, Guid.NewGuid(), Guid.Empty, "Motivo ficticio"));
    }
    [Fact]
    public async Task ClinicalCorrectionInvalidatesUseButDoesNotRewriteThePublishedSnapshot()
    {
        await using var test = await TechnicalTestContext.CreateAsync();
        var published = await test.Publications.PublishAsync(test.CustomerId, test.RecordId, await test.InputAsync()); Assert.True(published.Succeeded);
        var before = (await test.Clinical.Clinical.GetAsync(test.CustomerId, test.RecordId))!;
        test.Source.SubmissionId = Guid.NewGuid(); test.Source.Version = before.Version;
        test.Source.Right.Sphere = 1.375m; test.Source.CorrectionReason = "Corrección de transcripción ficticia";
        Assert.True((await test.Clinical.Clinical.CorrectAsync(test.CustomerId, test.RecordId, test.Source)).Succeeded);
        var old = (await test.Publications.GetAsync(test.CustomerId, published.Id!.Value))!;
        Assert.Equal(PublicationStatus.SourceChanged, old.Status); Assert.Equal(1.125m, old.Right.Sphere);
        var input = await test.InputAsync(); input.CorrectsId = old.Id; input.CorrectsVersion = old.Version; input.CorrectionReason = "Nueva versión técnica ficticia";
        var replacement = await test.Publications.PublishAsync(test.CustomerId, test.RecordId, input); Assert.True(replacement.Succeeded, replacement.Error);
        var latest = (await test.Publications.GetAsync(test.CustomerId, replacement.Id!.Value))!;
        Assert.Equal(PublicationStatus.Current, latest.Status); Assert.Equal(1.375m, latest.Right.Sphere);
        Assert.Equal(old.Id, latest.PreviousId);
        var superseded = (await test.Publications.GetAsync(test.CustomerId, old.Id))!;
        Assert.Equal(PublicationStatus.Superseded, superseded.Status); Assert.Equal(latest.Id, superseded.SupersededById);
        Assert.Equal(1.125m, superseded.Right.Sphere);
    }
    [Fact]
    public async Task RevocationIsAuditedIdempotentAndCannotCrossCustomers()
    {
        await using var test = await TechnicalTestContext.CreateAsync();
        var published = await test.Publications.PublishAsync(test.CustomerId, test.RecordId, await test.InputAsync()); Assert.True(published.Succeeded);
        var original = (await test.Publications.GetAsync(test.CustomerId, published.Id!.Value))!;
        Assert.Null(await test.Publications.GetAsync(Guid.NewGuid(), original.Id));
        Assert.Equal(TechnicalResultCode.NotFound, (await test.Publications.RevokeAsync(Guid.NewGuid(), original.Id, original.Version, "Ficticio")).Code);
        Assert.Equal(TechnicalResultCode.Conflict, (await test.Publications.RevokeAsync(test.CustomerId, original.Id, Guid.NewGuid(), "Ficticio")).Code);
        Assert.True((await test.Publications.RevokeAsync(test.CustomerId, original.Id, original.Version, "Revocación ficticia")).Succeeded);
        Assert.True((await test.Publications.RevokeAsync(test.CustomerId, original.Id, original.Version, "Revocación ficticia")).Succeeded);
        Assert.Equal(PublicationStatus.Revoked, (await test.Publications.GetAsync(test.CustomerId, original.Id))!.Status);
        await using var db = await test.Clinical.Factory.CreateDbContextAsync();
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "technical.publication.revoked").ToListAsync());
    }
    [Fact]
    public async Task ConfirmationAndSourceVersionAreMandatoryAndIdempotencyCannotReplaceContent()
    {
        await using var test = await TechnicalTestContext.CreateAsync(); var input = await test.InputAsync();
        input.Confirmed = false;
        Assert.Equal(TechnicalResultCode.Invalid, (await test.Publications.PublishAsync(test.CustomerId, test.RecordId, input)).Code);
        input.Confirmed = true; input.RecordVersion = Guid.NewGuid();
        Assert.Equal(TechnicalResultCode.Conflict, (await test.Publications.PublishAsync(test.CustomerId, test.RecordId, input)).Code);
        input = await test.InputAsync();
        var saved = await test.Publications.PublishAsync(test.CustomerId, test.RecordId, input); Assert.True(saved.Succeeded);
        input.Product = "Otro producto ficticio";
        Assert.Equal(TechnicalResultCode.Conflict, (await test.Publications.PublishAsync(test.CustomerId, test.RecordId, input)).Code);
        await using var db = await test.Clinical.Factory.CreateDbContextAsync(); Assert.Single(await db.TechnicalPublications.ToListAsync());
    }
}

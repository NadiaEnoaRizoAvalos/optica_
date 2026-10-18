using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Technical;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class TechnicalDomainTests
{
    private static TechnicalPublication Publication(EyeValues eye) => new(Guid.NewGuid(), Guid.NewGuid(),
        new(TechnicalPurpose.SpectacleLenses, true, false, "Cristal ficticio"),
        new(DateOnly.FromDateTime(DateTime.UtcNow), Guid.NewGuid(), "Fuente ficticia", null, null, eye, new()), "actor-ficticio", DateTime.UtcNow);
    [Fact]
    public void FabricationRequiresDeclaredSphereAxisForCylinderAndBaseForPrism()
    {
        Assert.Throws<ArgumentException>(() => Publication(new()));
        Assert.Throws<ArgumentException>(() => Publication(new(Sphere: 1, Cylinder: -1)));
        Assert.Throws<ArgumentException>(() => Publication(new(Sphere: 1, Prism: .125m)));
        Assert.Equal(0m, Publication(new(Sphere: 0, Cylinder: 0)).Right.Sphere);
        Assert.Equal(-1m, Publication(new(Sphere: 1, Cylinder: -1, Axis: 180)).Right.Cylinder);
    }
    [Fact]
    public void MountingKeepsTwoDecimalsAndDoesNotInferMonocularDistances()
    {
        var mounting = new MountingSpecification(rightHeightMm: 17.25m, rightMonocularDistanceMm: 31.12m);
        Assert.Null(mounting.LeftMonocularDistanceMm);
        Assert.Throws<ArgumentException>(() => new MountingSpecification(rightHeightMm: 17.251m));
        Assert.Throws<ArgumentException>(() => new MountingSpecification(rightHeightMm: -1));
        Assert.Throws<ArgumentException>(() => new TechnicalRequest(TechnicalPurpose.SpectacleLenses, true, false, "Ficticio", mounting: new(leftHeightMm: 15)));
    }
    [Fact]
    public void ContactPurposeRequiresExplicitProductFitForEveryIncludedEye()
    {
        Assert.Throws<ArgumentException>(() => new TechnicalRequest(TechnicalPurpose.ContactLenses, true, false, "Ficticio"));
        var fit = new ContactSelection(Guid.NewGuid(), 8.6m, 14.2m);
        Assert.Equal(fit, new TechnicalRequest(TechnicalPurpose.ContactLenses, true, false, "Ficticio", rightContact: fit).RightContact);
        Assert.Throws<ArgumentException>(() => new TechnicalRequest(TechnicalPurpose.ContactLenses, true, true, "Ficticio", rightContact: fit));
        Assert.Throws<ArgumentException>(() => new ContactSelection(Guid.NewGuid(), 8.601m, 14.2m));
    }
}

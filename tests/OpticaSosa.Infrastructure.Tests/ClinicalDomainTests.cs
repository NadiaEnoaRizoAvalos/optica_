using OpticaSosa.Domain.Clinical;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class ClinicalDomainTests
{
    [Fact]
    public void MeasurementsPreserveThreeDecimalsZeroAndAbsenceIndependently()
    {
        var right = new EyeMeasurement(Guid.NewGuid(), EyeSide.Right, new(1.125m, 0m, 180, null, 0.125m, "Nasal"));
        Assert.Equal(1125, right.SphereMilli);
        Assert.Equal(0m, right.ToValues().Cylinder);
        Assert.Null(right.ToValues().Addition);
        Assert.Equal(0.125m, right.ToValues().Prism);
        Assert.Equal(180, right.ToValues().Axis);
        var left = new EyeMeasurement(Guid.NewGuid(), EyeSide.Left, new(-2.375m));
        Assert.Equal(-2.375m, left.ToValues().Sphere);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(181)]
    public void AxisOutsideRangeIsRejected(int axis) =>
        Assert.Throws<ArgumentException>(() => new EyeMeasurement(Guid.NewGuid(), EyeSide.Right, new(Axis: axis)));

    [Fact]
    public void ExcessPrecisionAndUnknownLateralityAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new EyeMeasurement(Guid.NewGuid(), EyeSide.Right, new(1.0001m)));
        Assert.Throws<ArgumentException>(() => new EyeMeasurement(Guid.NewGuid(), (EyeSide)99, new()));
        Assert.Throws<ArgumentException>(() => EyeMeasurement.Scale(62.125m, 100, "distancia"));
    }

    [Fact]
    public void CorrectionRequiresReasonAndSnapshotsInformantWithoutChangingPreviousRevision()
    {
        var professional = new Professional(Guid.NewGuid(), new("Profesional ficticio", "MAT-FIC", "Ámbito ficticio"), "ficticio", DateTime.UtcNow);
        var record = new ClinicalRecord(Guid.NewGuid(), Guid.NewGuid(), null, ClinicalKind.MedicalPrescription);
        var data = new ClinicalData(new DateOnly(2026, 10, 15), professional.Id, "Documento ficticio", null, 62.25m, new(1.125m), new(-2.375m));
        var original = new ClinicalRevision(Guid.NewGuid(), record, data, professional, "ficticio", DateTime.UtcNow, null);
        Assert.Equal(6225, original.PupillaryDistanceHundredths);
        record.AddRevision();
        Assert.Throws<ArgumentException>(() => new ClinicalRevision(Guid.NewGuid(), record, data, professional, "ficticio", DateTime.UtcNow, " "));
        professional.Update(new("Nombre actualizado ficticio", null, null), true);
        var corrected = new ClinicalRevision(Guid.NewGuid(), record, data with { Notes = "Corrección ficticia" }, professional, "ficticio", DateTime.UtcNow, "Error de transcripción ficticio");
        Assert.Equal(2, corrected.Number);
        Assert.Equal("Profesional ficticio", original.ProfessionalName);
        Assert.Equal("Nombre actualizado ficticio", corrected.ProfessionalName);
        Assert.Null(original.Notes);
    }

    [Fact]
    public void VisualHistoryRequiresReportedTextAndRejectsPrescriptionFields()
    {
        var professional = new Professional(Guid.NewGuid(), new("Profesional ficticio", null, null), "ficticio", DateTime.UtcNow);
        var record = new ClinicalRecord(Guid.NewGuid(), Guid.NewGuid(), null, ClinicalKind.VisualHistory);
        var data = new ClinicalData(new DateOnly(2026, 10, 15), professional.Id, "Fuente ficticia", null, null, new(), new());
        Assert.Throws<ArgumentException>(() => new ClinicalRevision(Guid.NewGuid(), record, data, professional, "ficticio", DateTime.UtcNow, null));
        Assert.Throws<ArgumentException>(() => new ClinicalRevision(Guid.NewGuid(), record, data with { Notes = "Informado ficticio", Right = new(0m) }, professional, "ficticio", DateTime.UtcNow, null));
    }
}

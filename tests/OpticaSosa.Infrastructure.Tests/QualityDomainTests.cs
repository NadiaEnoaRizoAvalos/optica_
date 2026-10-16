using OpticaSosa.Domain.Work;
using Xunit;
namespace OpticaSosa.Infrastructure.Tests;
public sealed class QualityDomainTests
{
    private static OpticalWork Work() { var w = new OpticalWork(Guid.NewGuid(), Guid.NewGuid(), OpticalWorkType.PrescriptionGlasses, "Producto ficticio", "Armazón ficticio", null, "usuario-ficticio", DateTime.UtcNow); w.Transition("prepare"); w.Transition("control"); return w; }
    [Theory]
    [InlineData(CheckStatus.Pending)] [InlineData(CheckStatus.Observed)] [InlineData(CheckStatus.NotApplicable)]
    public void CannotApproveIncompleteObservedOrInapplicableBypass(CheckStatus status)
    {
        var w = Work(); var rules = QualityCatalog.For(w.Type, QualityStage.Technical);
        var values = rules.Select((r, n) => new CheckValue(r.Key, n == 0 ? status : CheckStatus.Conforming, "Motivo ficticio")).ToArray();
        Assert.ThrowsAny<Exception>(() => new QualityControl(Guid.NewGuid(), w, QualityStage.Technical, rules, values, true, "ficticio", ResponsibleRole.Professional, DateTime.UtcNow, null, Guid.Empty));
    }
    [Theory]
    [InlineData("")] [InlineData(" ")]
    public void ApprovalNeedsIdentifiedResponsible(string actor)
    {
        var w = Work(); var rules = QualityCatalog.For(w.Type, QualityStage.Technical);
        Assert.Throws<ArgumentException>(() => new QualityControl(Guid.NewGuid(), w, QualityStage.Technical, rules,
            rules.Select(r => new CheckValue(r.Key, CheckStatus.Conforming)).ToArray(), true, actor, ResponsibleRole.Professional, DateTime.UtcNow, null, Guid.Empty));
    }
    [Fact]
    public void ReadyRequiresBothStagesAndChangesInvalidateWithoutDeletingHistory()
    {
        var w = Work(); Assert.Throws<InvalidOperationException>(() => w.Transition("deliver"));
        var approved = new List<QualityControl>();
        foreach (var stage in Enum.GetValues<QualityStage>()) {
            var rules = QualityCatalog.For(w.Type, stage); var c = new QualityControl(Guid.NewGuid(), w, stage, rules,
                rules.Select(r => new CheckValue(r.Key, CheckStatus.Conforming)).ToArray(), true, "ficticio", ResponsibleRole.Professional, DateTime.UtcNow, null, Guid.Empty);
            w.Apply(c); approved.Add(c); if (stage == QualityStage.Technical) Assert.Equal(WorkStatus.PendingControl, w.Status);
        }
        Assert.Equal(WorkStatus.ReadyForDelivery, w.Status); var oldVersion = w.Version;
        w.Edit("Producto ficticio rectificado", w.Frame, null);
        Assert.NotEqual(oldVersion, w.Version); Assert.False(w.TechnicalApproved); Assert.False(w.DeliveryApproved); Assert.Equal(2, w.Round);
        Assert.All(approved, c => Assert.Equal(ControlStatus.Approved, c.Status));
        Assert.Throws<InvalidOperationException>(() => w.Apply(approved[0]));
    }
    [Fact]
    public void NoApplyRequiresReasonOnlyForNonApplicableWorkAndChecklistCannotRepeatKeys()
    {
        var w = new OpticalWork(Guid.NewGuid(), Guid.NewGuid(), OpticalWorkType.FrameOnly, "Ficticio", "Ficticio", null, "ficticio", DateTime.UtcNow);
        var rules = QualityCatalog.For(w.Type, QualityStage.Technical);
        var values = rules.Select(r => new CheckValue(r.Key, r.Applicable ? CheckStatus.Conforming : CheckStatus.NotApplicable, r.Applicable ? null : "No incluye cristales ni graduación")).ToArray();
        Assert.Equal(ControlStatus.Approved, new QualityControl(Guid.NewGuid(), w, QualityStage.Technical, rules, values, true, "ficticio", ResponsibleRole.Professional, DateTime.UtcNow, null, Guid.Empty).Status);
        Assert.Throws<ArgumentException>(() => new QualityControl(Guid.NewGuid(), w, QualityStage.Technical, rules, values.Select(v => v with { Notes = null }).ToArray(), true, "ficticio", ResponsibleRole.Professional, DateTime.UtcNow, null, Guid.Empty));
        values[1] = values[0];
        Assert.Throws<ArgumentException>(() => new QualityControl(Guid.NewGuid(), w, QualityStage.Technical, rules, values, true, "ficticio", ResponsibleRole.Professional, DateTime.UtcNow, null, Guid.Empty));
    }
}

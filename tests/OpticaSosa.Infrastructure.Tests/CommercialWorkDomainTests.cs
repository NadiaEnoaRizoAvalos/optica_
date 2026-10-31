using OpticaSosa.Domain.Delivery;
using OpticaSosa.Domain.Work;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class CommercialWorkDomainTests
{
    [Fact]
    public void ReplacedWorkKeepsItsApprovalEvidenceButCannotReceiveAnotherControl()
    {
        var work = new CommercialWork(Guid.NewGuid(), Guid.NewGuid(), null, null);
        var materialHash = new string('A', 64);
        work.RecordControl(QualityStage.Technical, true, materialHash);
        work.RecordControl(QualityStage.Delivery, true, materialHash);
        work.Retire();
        Assert.False(work.IsCurrent); Assert.Equal(materialHash, work.TechnicalMaterialHash); Assert.Equal(materialHash, work.DeliveryMaterialHash);
        Assert.Throws<ArgumentException>(() => work.RecordControl(QualityStage.Technical, true, materialHash));
    }
}

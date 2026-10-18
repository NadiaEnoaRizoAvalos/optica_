using OpticaSosa.Domain.Laboratory;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class LaboratoryDomainTests
{
    [Fact]
    public void ReceivingAndCancellingPreservePartialQuantitiesAndRequireRecordedDispatchAndConfirmation()
    {
        var lab = new LaboratoryOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 5, null, "actor-ficticio", DateTime.UtcNow);
        Assert.Throws<ArgumentException>(() => lab.Receive(1, 0, DateTime.UtcNow));
        lab.RecordDispatch("actor-ficticio", DateTime.UtcNow, "Referencia ficticia"); lab.Receive(2, 1, DateTime.UtcNow);
        Assert.Equal(3, lab.PendingQuantity); Assert.Equal(1, lab.UsableReceivedQuantity); Assert.Equal(LaboratoryStatus.PartiallyReceived, lab.Status);
        Assert.Throws<ArgumentException>(() => lab.Cancel(3, null));
        Assert.Throws<ArgumentException>(() => lab.Cancel(4, "Confirmación ficticia"));
        lab.Cancel(3, "Confirmación ficticia"); Assert.Equal(2, lab.ReceivedQuantity); Assert.Equal(1, lab.QuarantinedQuantity);
        Assert.Equal(3, lab.CancelledQuantity); Assert.Equal(LaboratoryStatus.Closed, lab.Status);
        Assert.Throws<ArgumentException>(() => lab.Receive(1, 0, DateTime.UtcNow));
    }
    [Fact]
    public void ExportIsNotAStateTransitionAndClosedOrdersCannotBeRescheduledOrResent()
    {
        var lab = new LaboratoryOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 1, new(2026, 11, 1), "actor-ficticio", DateTime.UtcNow);
        Assert.Equal(LaboratoryStatus.Authorized, lab.Status); Assert.Null(lab.SentAtUtc);
        lab.Reschedule(new(2026, 11, 2)); lab.Cancel(1, null);
        Assert.Equal(LaboratoryStatus.Cancelled, lab.Status);
        Assert.Throws<ArgumentException>(() => lab.Reschedule(null));
        Assert.Throws<ArgumentException>(() => lab.RecordDispatch("actor-ficticio", DateTime.UtcNow, "Ficticio"));
    }
}

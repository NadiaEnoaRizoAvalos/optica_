using OpticaSosa.Application.Inventory;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Inventory;

namespace OpticaSosa.Infrastructure.Inventory;

internal static class TechnicalProjectionFactory
{
    internal static OrderTechnicalProjection Create(Guid orderId, Guid lineId, OrderTechnicalAssignment assignment, PublishedTechnical source)
    {
        var p = source.Publication; var r = p.Request; var m = r.Mounting;
        var right = assignment.Eyes.HasFlag(TechnicalEyes.Right); var left = assignment.Eyes.HasFlag(TechnicalEyes.Left);
        return new(orderId, lineId, assignment.Id, p.Id, p.SourceRevisionId, source.RevisionNumber, source.Status, assignment.Eyes,
            r.Purpose, r.Product, r.Material, r.RefractiveIndex, r.Treatment, right ? p.Right : new EyeValues(), left ? p.Left : new EyeValues(),
            p.PupillaryDistanceHundredths / 100m, new(right ? m.RightHeightMm : null, left ? m.LeftHeightMm : null,
                right ? m.RightMonocularDistanceMm : null, left ? m.LeftMonocularDistanceMm : null, m.FrameVariantId, m.FrameReference, m.Instructions),
            right ? r.RightContact : null, left ? r.LeftContact : null);
    }
}

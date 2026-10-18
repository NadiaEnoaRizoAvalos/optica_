namespace OpticaSosa.Domain.Work;

public enum OpticalWorkType { PrescriptionGlasses, FrameOnly, Repair, ContactLenses, Accessory, MechanicalRepair, OpticalRepair, CommercialGlasses, CommercialFrame }
public enum WorkStatus { Registered, InPreparation, PendingControl, Observed, ReadyForDelivery, Delivered }
public enum QualityStage { Technical, Delivery }
public enum CheckStatus { Pending, Conforming, Observed, NotApplicable }
public enum ControlStatus { Draft, Observed, Approved }
public enum ResponsibleRole { Professional, Sales }
public sealed record CheckDefinition(string Key, string Label, bool Applicable = true, bool Required = true, bool Essential = false);
public sealed record CheckValue(string Key, CheckStatus Status, string? Notes = null);

public static class QualityCatalog
{
    public static bool RequiresProfessional(OpticalWorkType type) => type is OpticalWorkType.PrescriptionGlasses or OpticalWorkType.Repair
        or OpticalWorkType.ContactLenses or OpticalWorkType.OpticalRepair or OpticalWorkType.CommercialGlasses;
    public static bool RequiresFrame(OpticalWorkType type) => type is not (OpticalWorkType.ContactLenses or OpticalWorkType.Accessory);
    public static IReadOnlyList<CheckDefinition> For(OpticalWorkType type, QualityStage stage)
    {
        if (!Enum.IsDefined(type) || !Enum.IsDefined(stage)) throw new ArgumentException("Tipo de pedido o control inválido.");
        // Los tipos históricos 0–2 conservan su checklist y snapshots. La matriz comercial usa tipos explícitos nuevos.
        if ((int)type >= 3) return CommercialQualityCatalog.For(type, stage);
        if (stage == QualityStage.Delivery) return [
            new("customer", "Identificación del cliente", Essential: true), new("order", "Correspondencia del pedido", Essential: true),
            new("prepared", "Producto preparado"), new("accessories", "Accesorios incluidos"),
            new("documents", "Documentación correspondiente"), new("condition", "Estado general del producto", Essential: true),
            new("delivery", "Condiciones para realizar la entrega", Essential: true)];
        bool lenses = type != OpticalWorkType.FrameOnly;
        bool prescription = type == OpticalWorkType.PrescriptionGlasses;
        return [new("prescription", "Correspondencia entre pedido y receta", prescription, prescription, prescription),
            new("power", "Graduación y especificaciones según pedido", prescription, prescription, prescription),
            new("lenses", "Tipo de cristales solicitado", lenses, lenses), new("frame", "Armazón correspondiente", Essential: true),
            new("mount", "Montaje correcto", lenses, lenses), new("alignment", "Centrado y alineación", prescription, prescription),
            new("lens-state", "Estado físico de los cristales", lenses, lenses), new("frame-state", "Estado físico del armazón", Essential: true),
            new("defects", "Ausencia de defectos visibles", Essential: true), new("final", "Observaciones finales revisadas")];
    }
}

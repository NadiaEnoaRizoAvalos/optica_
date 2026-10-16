using OpticaSosa.Domain.Work;
namespace OpticaSosa.Web.Pages.Work;
public static class WorkDisplay
{
    public static string Status(WorkStatus s) => s switch { WorkStatus.Registered => "Registrado", WorkStatus.InPreparation => "En preparación",
        WorkStatus.PendingControl => "Pendiente de control", WorkStatus.Observed => "Observado", WorkStatus.ReadyForDelivery => "Listo para entrega", _ => "Entregado" };
    public static string Type(OpticalWorkType t) => t switch { OpticalWorkType.PrescriptionGlasses => "Anteojos con receta", OpticalWorkType.FrameOnly => "Sólo armazón", _ => "Reparación" };
    public static string Stage(QualityStage s) => s == QualityStage.Technical ? "Control técnico" : "Control previo a la entrega";
    public static string Check(CheckStatus s) => s switch { CheckStatus.Pending => "Pendiente", CheckStatus.Conforming => "Conforme", CheckStatus.Observed => "Observado", _ => "No aplica" };
    public static string Control(ControlStatus s) => s switch { ControlStatus.Draft => "Borrador", ControlStatus.Observed => "Observado", _ => "Aprobado" };
}

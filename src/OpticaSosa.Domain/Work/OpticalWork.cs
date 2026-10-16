namespace OpticaSosa.Domain.Work;

public sealed class OpticalWork
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? PrescriptionId { get; private set; }
    public Guid? PrescriptionRevisionId { get; private set; }
    public OpticalWorkType Type { get; private set; }
    public string Product { get; private set; } = "";
    public string Frame { get; private set; } = "";
    public int Round { get; private set; } = 1;
    public bool TechnicalApproved { get; private set; }
    public bool DeliveryApproved { get; private set; }
    public WorkStatus Status { get; private set; }
    public Guid Version { get; private set; }
    public string CreatedByUserId { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    private OpticalWork() { }
    public OpticalWork(Guid id, Guid customerId, OpticalWorkType type, string product, string frame, Guid? prescriptionId, string actor, DateTime now, Guid? prescriptionRevisionId = null)
    {
        if (id == Guid.Empty || customerId == Guid.Empty || string.IsNullOrWhiteSpace(actor) || !Enum.IsDefined(type)) throw new ArgumentException("Pedido inválido.");
        if (prescriptionId.HasValue != prescriptionRevisionId.HasValue) throw new ArgumentException("La receta debe incluir su versión de referencia.");
        Id = id; CustomerId = customerId; Type = type; CreatedByUserId = actor; CreatedAtUtc = now;
        Product = Text(product, 200); Frame = Text(frame, 200); PrescriptionId = prescriptionId; PrescriptionRevisionId = prescriptionRevisionId; Touch();
    }
    public void Edit(string product, string frame, Guid? prescriptionId, Guid? prescriptionRevisionId = null)
    {
        if (Status == WorkStatus.Delivered) throw new InvalidOperationException("Un pedido entregado conserva su historial; registrá un nuevo trabajo.");
        if (prescriptionId.HasValue != prescriptionRevisionId.HasValue) throw new ArgumentException("La receta debe incluir su versión de referencia.");
        var newProduct = Text(product, 200); var newFrame = Text(frame, 200);
        if (Product == newProduct && Frame == newFrame && PrescriptionId == prescriptionId && PrescriptionRevisionId == prescriptionRevisionId) return;
        Product = newProduct; Frame = newFrame; PrescriptionId = prescriptionId; PrescriptionRevisionId = prescriptionRevisionId;
        Round++; TechnicalApproved = DeliveryApproved = false;
        if (Status != WorkStatus.Registered) Status = WorkStatus.InPreparation;
        Touch();
    }
    public void Transition(string command)
    {
        Status = (Status, command) switch
        {
            (WorkStatus.Registered, "prepare") => WorkStatus.InPreparation,
            (WorkStatus.InPreparation, "control") => WorkStatus.PendingControl,
            (WorkStatus.Observed, "correct") => WorkStatus.InPreparation,
            (WorkStatus.ReadyForDelivery, "deliver") when TechnicalApproved && DeliveryApproved => WorkStatus.Delivered,
            _ => throw new InvalidOperationException("Transición no permitida. Se deben completar y aprobar los controles aplicables antes de entregar.")
        };
        if (command == "correct") { Round++; TechnicalApproved = DeliveryApproved = false; }
        Touch();
    }
    public void Apply(QualityControl control)
    {
        if (control.WorkId != Id || control.Round != Round || Status is not (WorkStatus.PendingControl or WorkStatus.ReadyForDelivery))
            throw new InvalidOperationException("El pedido no está disponible para este control. Recargá su ficha.");
        if (control.Stage == QualityStage.Technical) TechnicalApproved = control.Status == ControlStatus.Approved;
        else DeliveryApproved = control.Status == ControlStatus.Approved;
        Status = control.Status == ControlStatus.Observed ? WorkStatus.Observed
            : TechnicalApproved && DeliveryApproved ? WorkStatus.ReadyForDelivery : WorkStatus.PendingControl;
        Touch();
    }
    public void Invalidate()
    {
        if (Status == WorkStatus.Delivered) return;
        Round++; TechnicalApproved = DeliveryApproved = false;
        if (Status != WorkStatus.Registered && Status != WorkStatus.InPreparation) Status = WorkStatus.PendingControl;
        Touch();
    }
    private void Touch() => Version = Guid.NewGuid();
    public static string Text(string? value, int max)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result) || result.Length > max || result.Any(c => char.IsControl(c) && c is not '\r' and not '\n'))
            throw new ArgumentException($"El campo es obligatorio y admite hasta {max} caracteres.");
        return result;
    }
}

public sealed class WorkTransition
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkId { get; private set; }
    public WorkStatus From { get; private set; }
    public WorkStatus To { get; private set; }
    public string ActorId { get; private set; } = "";
    public DateTime AtUtc { get; private set; }
    public string Action { get; private set; } = "";
    private WorkTransition() { }
    public WorkTransition(OpticalWork work, WorkStatus from, string actor, DateTime now, string action)
    { WorkId = work.Id; From = from; To = work.Status; ActorId = actor; AtUtc = now; Action = action; }
}

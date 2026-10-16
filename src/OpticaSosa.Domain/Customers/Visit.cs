namespace OpticaSosa.Domain.Customers;

public enum VisitStatus { InProgress, Completed, Cancelled }

public sealed class Visit
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Reason { get; private set; } = "";
    public string? AdministrativeNotes { get; private set; }
    public string RecordedByUserId { get; private set; } = "";
    public string? EndedByUserId { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public bool WasRecurrent { get; private set; }
    public VisitStatus Status { get; private set; }
    public Guid Version { get; private set; }

    private Visit() { }

    public Visit(Guid id, Guid customerId, string reason, string? notes, string actorId, DateTime utcNow, bool wasRecurrent)
    {
        if (id == Guid.Empty || customerId == Guid.Empty || string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("Identificación de visita inválida.");
        Id = id;
        CustomerId = customerId;
        Reason = CustomerData.Required(reason, 100, "motivo");
        AdministrativeNotes = CustomerData.Optional(notes, 500, "observaciones administrativas");
        RecordedByUserId = actorId;
        StartedAtUtc = utcNow;
        WasRecurrent = wasRecurrent;
        Version = Guid.NewGuid();
    }

    public void Close(bool completed, string actorId, DateTime utcNow)
    {
        if (Status != VisitStatus.InProgress) throw new InvalidOperationException("La visita ya está cerrada.");
        if (utcNow < StartedAtUtc || string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Cierre de visita inválido.");
        Status = completed ? VisitStatus.Completed : VisitStatus.Cancelled;
        EndedAtUtc = utcNow;
        EndedByUserId = actorId;
        Version = Guid.NewGuid();
    }
}

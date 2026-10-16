namespace OpticaSosa.Domain.Clinical;

public enum AttachmentState { Pending, Available, Failed, Retired }

public sealed class ClinicalAttachment
{
    public Guid Id { get; private set; }
    public Guid RevisionId { get; private set; }
    public string OriginalName { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long Length { get; private set; }
    public string Sha256 { get; private set; } = "";
    public string CreatedByUserId { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public AttachmentState State { get; private set; }
    public Guid Version { get; private set; }
    public string? RetiredByUserId { get; private set; }
    public DateTime? RetiredAtUtc { get; private set; }
    public string? RetirementReason { get; private set; }

    private ClinicalAttachment() { }
    public ClinicalAttachment(Guid id, Guid revisionId, string originalName, string contentType, long length,
        string sha256, string actor, DateTime now)
    {
        if (id == Guid.Empty || revisionId == Guid.Empty || length <= 0 || sha256.Length != 64 || string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("Metadatos de archivo inválidos.");
        Id = id; RevisionId = revisionId; OriginalName = originalName; ContentType = contentType;
        Length = length; Sha256 = sha256; CreatedByUserId = actor; CreatedAtUtc = now;
        Version = Guid.NewGuid();
    }
    public void MarkAvailable() { State = AttachmentState.Available; Version = Guid.NewGuid(); }
    public void MarkFailed() { State = AttachmentState.Failed; Version = Guid.NewGuid(); }
    public void Retry() { State = AttachmentState.Pending; Version = Guid.NewGuid(); }
    public void Retire(string reason, string actor, DateTime now)
    {
        if (State != AttachmentState.Available) throw new InvalidOperationException("El archivo no está disponible.");
        RetirementReason = ClinicalText.Required(reason, 500, "motivo de retiro");
        RetiredByUserId = actor; RetiredAtUtc = now;
        State = AttachmentState.Retired; Version = Guid.NewGuid();
    }
}

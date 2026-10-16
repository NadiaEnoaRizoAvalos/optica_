namespace OpticaSosa.Domain.Clinical;

public enum ClinicalKind { MedicalPrescription, OpticalPrescription, VisualHistory }

public sealed class ClinicalRecord
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? VisitId { get; private set; }
    public ClinicalKind Kind { get; private set; }
    public int RevisionNumber { get; private set; }
    public Guid Version { get; private set; }

    private ClinicalRecord() { }
    public ClinicalRecord(Guid id, Guid customerId, Guid? visitId, ClinicalKind kind)
    {
        if (id == Guid.Empty || customerId == Guid.Empty || visitId == Guid.Empty || !Enum.IsDefined(kind))
            throw new ArgumentException("Identificación o tipo de registro inválido.");
        Id = id;
        CustomerId = customerId;
        VisitId = visitId;
        Kind = kind;
        Touch();
    }

    public int AddRevision() { Touch(); return ++RevisionNumber; }
    public void Touch() => Version = Guid.NewGuid();
}

public sealed record ClinicalData(DateOnly Date, Guid ProfessionalId, string Source, string? Notes,
    decimal? PupillaryDistance, EyeValues Right, EyeValues Left);

public sealed class ClinicalRevision
{
    public Guid Id { get; private set; }
    public Guid RecordId { get; private set; }
    public int Number { get; private set; }
    public DateOnly Date { get; private set; }
    public Guid ProfessionalId { get; private set; }
    public string ProfessionalName { get; private set; } = "";
    public string? ProfessionalRegistration { get; private set; }
    public string? ProfessionalJurisdiction { get; private set; }
    public string Source { get; private set; } = "";
    public string? Notes { get; private set; }
    public int? PupillaryDistanceHundredths { get; private set; }
    public string? CorrectionReason { get; private set; }
    public string RecordedByUserId { get; private set; } = "";
    public DateTime RecordedAtUtc { get; private set; }
    public List<EyeMeasurement> Eyes { get; private set; } = [];

    private ClinicalRevision() { }
    public ClinicalRevision(Guid id, ClinicalRecord record, ClinicalData data, Professional professional,
        string actor, DateTime now, string? correctionReason)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(actor) || data.Date == default ||
            data.ProfessionalId != professional.Id || !professional.IsActive)
            throw new ArgumentException("Indicá fecha y profesional activo válidos.");
        Id = id;
        RecordId = record.Id;
        Number = record.RevisionNumber + 1;
        Date = data.Date;
        ProfessionalId = professional.Id;
        ProfessionalName = professional.FullName;
        ProfessionalRegistration = professional.Registration;
        ProfessionalJurisdiction = professional.Jurisdiction;
        Source = ClinicalText.Required(data.Source, 200, "procedencia");
        Notes = record.Kind == ClinicalKind.VisualHistory
            ? ClinicalText.Required(data.Notes, 4000, "antecedente informado")
            : ClinicalText.Optional(data.Notes, 4000, "observaciones");
        CorrectionReason = Number > 1 ? ClinicalText.Required(correctionReason, 500, "motivo de corrección") : null;
        RecordedByUserId = actor;
        RecordedAtUtc = now;
        if (record.Kind == ClinicalKind.VisualHistory)
        {
            if (data.PupillaryDistance is not null || data.Right != new EyeValues() || data.Left != new EyeValues())
                throw new ArgumentException("Las graduaciones corresponden a recetas, no a antecedentes.");
        }
        else
        {
            if (data.PupillaryDistance is < 0) throw new ArgumentException("La distancia pupilar no puede ser negativa.");
            PupillaryDistanceHundredths = EyeMeasurement.Scale(data.PupillaryDistance, 100, "distancia pupilar");
            Eyes = [new(id, EyeSide.Right, data.Right), new(id, EyeSide.Left, data.Left)];
        }
    }

    public ClinicalData ToData() => new(Date, ProfessionalId, Source, Notes, PupillaryDistanceHundredths / 100m,
        Eyes.SingleOrDefault(x => x.Side == EyeSide.Right)?.ToValues() ?? new(),
        Eyes.SingleOrDefault(x => x.Side == EyeSide.Left)?.ToValues() ?? new());
}

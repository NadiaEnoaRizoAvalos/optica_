namespace OpticaSosa.Domain.Work;

public sealed class QualityControl
{
    public Guid Id { get; private set; }
    public Guid WorkId { get; private set; }
    public OpticalWorkType WorkType { get; private set; }
    public int Round { get; private set; }
    public QualityStage Stage { get; private set; }
    public ControlStatus Status { get; private set; }
    public string ActorId { get; private set; } = "";
    public ResponsibleRole Role { get; private set; }
    public DateTime AtUtc { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public string? Notes { get; private set; }
    public Guid TemplateVersion { get; private set; }
    public string? MaterialHash { get; private set; }
    public List<QualityCheckItem> Items { get; private set; } = [];
    private QualityControl() { }
    public QualityControl(Guid id, OpticalWork work, QualityStage stage, IReadOnlyList<CheckDefinition> rules,
        IReadOnlyList<CheckValue> values, bool approve, string actor, ResponsibleRole role, DateTime now, string? notes, Guid templateVersion, string? materialHash = null)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(actor) || !Enum.IsDefined(stage) || !Enum.IsDefined(role) ||
            (stage == QualityStage.Technical && QualityCatalog.RequiresProfessional(work.Type) && role != ResponsibleRole.Professional)) throw new ArgumentException("Responsable no autorizado para el control.");
        if (values.Count != rules.Count || values.Select(x => x.Key).Distinct().Count() != rules.Count || values.Any(x => rules.All(r => r.Key != x.Key)))
            throw new ArgumentException("El checklist debe incluir todas las verificaciones vigentes sin duplicados.");
        if (QualityCatalog.For(work.Type, stage).Any(x => x.Essential && !rules.Any(r => r.Key == x.Key && r.Applicable && r.Required)))
            throw new ArgumentException("El checklist no puede omitir verificaciones esenciales del trabajo.");
        if (notes?.Length > 2000) throw new ArgumentException("Observaciones: hasta 2000 caracteres.");
        Id = id; WorkId = work.Id; WorkType = work.Type; Round = work.Round; Stage = stage; ActorId = actor; Role = role; AtUtc = now;
        if (materialHash is not null && (materialHash.Length != 64 || materialHash.Any(x => !Uri.IsHexDigit(x)))) throw new ArgumentException("Referencia de materiales inválida.");
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(); TemplateVersion = templateVersion; MaterialHash = materialHash;
        foreach (var rule in rules)
        {
            var value = values.Single(x => x.Key == rule.Key);
            if (!Enum.IsDefined(value.Status) || value.Notes?.Length > 1000) throw new ArgumentException("Estado u observaciones de ítem inválidos.");
            if (value.Status == CheckStatus.NotApplicable && (rule.Applicable || string.IsNullOrWhiteSpace(value.Notes)))
                throw new ArgumentException("No aplica sólo se permite para un ítem no aplicable al tipo de pedido y exige justificación.");
            Items.Add(new(id, rule, value));
        }
        var observed = Items.Any(x => x.Status == CheckStatus.Observed);
        var complete = Items.All(x => x.Applicable ? (!x.Required || x.Status == CheckStatus.Conforming) : x.Status == CheckStatus.NotApplicable);
        if (approve && (observed || !complete)) throw new InvalidOperationException("No se puede aprobar: existen verificaciones obligatorias pendientes, observadas o sin justificación de No aplica.");
        Status = observed ? ControlStatus.Observed : approve ? ControlStatus.Approved : ControlStatus.Draft;
        ApprovedAtUtc = Status == ControlStatus.Approved ? now : null;
    }
}

public sealed class QualityCheckItem
{
    public Guid ControlId { get; private set; }
    public string Key { get; private set; } = "";
    public string Label { get; private set; } = "";
    public bool Applicable { get; private set; }
    public bool Required { get; private set; }
    public CheckStatus Status { get; private set; }
    public string? Notes { get; private set; }
    private QualityCheckItem() { }
    public QualityCheckItem(Guid id, CheckDefinition rule, CheckValue value)
    { ControlId = id; Key = rule.Key; Label = rule.Label; Applicable = rule.Applicable; Required = rule.Required; Status = value.Status; Notes = value.Notes?.Trim(); }
}

public sealed class QualityTemplate
{
    public OpticalWorkType Type { get; set; }
    public QualityStage Stage { get; set; }
    public Guid Version { get; set; }
    public List<QualityTemplateItem> Items { get; set; } = [];
}
public sealed class QualityTemplateItem
{
    public OpticalWorkType Type { get; set; }
    public QualityStage Stage { get; set; }
    public string Key { get; set; } = "";
    public bool Required { get; set; }
}

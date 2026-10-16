namespace OpticaSosa.Application.Clinical;

public sealed record ClinicalLink(Guid Id, Guid CustomerId, string CustomerName, string Kind, DateOnly Date,
    Guid ProfessionalId, string ProfessionalName, int Revision, string AuthorId);
public sealed record ClinicalLinks(IReadOnlyList<ClinicalLink> Items, int Page, bool HasMore);
public sealed record CustomerClinicalOverview(int Recipes, int Histories, DateOnly? LastDate, IReadOnlyList<ProfessionalDetails> Professionals);
public interface IClinicalDirectory
{
    Task<CustomerClinicalOverview> CustomerAsync(Guid id, CancellationToken ct = default);
    Task<ClinicalLinks> RecordsAsync(Guid? customerId = null, Guid? professionalId = null, bool? histories = null, int page = 1, CancellationToken ct = default);
}

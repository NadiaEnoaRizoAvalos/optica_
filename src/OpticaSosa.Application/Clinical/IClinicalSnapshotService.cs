namespace OpticaSosa.Application.Clinical;

public sealed record ClinicalSnapshot(string Directory, int Files, string Status);
public interface IClinicalSnapshotService
{
    Task<ClinicalSnapshot> CreateAsync(CancellationToken ct = default);
}

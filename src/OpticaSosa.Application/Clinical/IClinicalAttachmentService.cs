using OpticaSosa.Domain.Clinical;

namespace OpticaSosa.Application.Clinical;

public sealed record AttachmentPolicyDetails(int PhotoMiB, int PdfMiB, Guid Version, int TechnicalRequestMiB);
public sealed record AttachmentUpload(Guid SubmissionId, Guid RevisionId, Guid RecordVersion, string OriginalName,
    string ContentType, long Length, Stream Content);
public sealed record AttachmentDetails(Guid Id, string OriginalName, string ContentType, long Length, string Sha256,
    DateTime CreatedAtUtc, AttachmentState State, Guid Version, string? RetirementReason);
public sealed record AttachmentPage(IReadOnlyList<AttachmentDetails> Items, int Page, bool HasMore);
public sealed record AttachmentDownload(Stream Content, string FileName, string ContentType);
public sealed record ImageOptimization(byte[]? Bytes, string? FileName, string? Error);

public interface IClinicalAttachmentService
{
    Task<AttachmentPage> ListAsync(Guid customerId, Guid recordId, Guid revisionId, int page = 1, CancellationToken ct = default);
    Task<ClinicalResult> UploadAsync(Guid customerId, Guid recordId, AttachmentUpload upload, CancellationToken ct = default);
    Task<AttachmentDownload?> DownloadAsync(Guid customerId, Guid recordId, Guid attachmentId, CancellationToken ct = default);
    Task<ClinicalResult> RetireAsync(Guid customerId, Guid recordId, Guid attachmentId, Guid version, string reason, CancellationToken ct = default);
    Task<ClinicalResult> RecoverAsync(Guid customerId, Guid recordId, Guid attachmentId, Guid version, CancellationToken ct = default);
    Task<ImageOptimization> OptimizeAsync(Guid customerId, Guid recordId, AttachmentUpload upload, CancellationToken ct = default);
}

public interface IClinicalFilePolicyService
{
    Task<AttachmentPolicyDetails> GetAsync(CancellationToken ct = default);
    Task<ClinicalResult> SaveAsync(int photoMiB, int pdfMiB, Guid version, CancellationToken ct = default);
}

public interface IStorageCapacity
{
    long AvailableBytes { get; }
}

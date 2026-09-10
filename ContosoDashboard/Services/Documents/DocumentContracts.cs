using System.Security.Claims;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services.Documents;

public sealed record DocumentUploadRequest(
    string Title,
    string Category,
    string? Description,
    int? ProjectId,
    int? TaskId,
    string? Tags,
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long Length);

public sealed record DocumentUploadResult(
    bool Success,
    int? DocumentId,
    string LifecycleStatus,
    string ScanStatus,
    string StatusMessage,
    string? ErrorMessage);

public sealed record DocumentSummaryDto(
    int DocumentId,
    string Title,
    string Category,
    string OriginalFileName,
    long FileSizeBytes,
    DocumentLifecycleStatus LifecycleStatus,
    DocumentVisibility Visibility,
    DocumentScanStatus ScanStatus,
    DateTime UploadedUtc);

public sealed record DocumentDetailDto(
    int DocumentId,
    string Title,
    string Category,
    string? Description,
    string? Tags,
    string OriginalFileName,
    long FileSizeBytes,
    DocumentLifecycleStatus LifecycleStatus,
    DocumentVisibility Visibility,
    DocumentScanStatus ScanStatus,
    DateTime UploadedUtc);

public sealed record DocumentQuery(int? ProjectId = null);

public sealed record ScanJobMessage(
    Guid ScanJobId,
    int DocumentId,
    int VersionNumber,
    string ContentHash,
    string StoragePath,
    string IdempotencyKey,
    int SchemaVersion = 1);

public sealed record ScanResultMessage(
    Guid ScanJobId,
    int DocumentId,
    int VersionNumber,
    string ContentHash,
    string IdempotencyKey,
    ScanVerdict Verdict,
    string ScannerVersion,
    string? ScannerCode = null,
    int SchemaVersion = 1);

public enum ScanVerdict { Clean, Flagged, TransientFailure, PermanentFailure, Malformed }

public interface IDocumentService
{
    Task<DocumentUploadResult> UploadAsync(DocumentUploadRequest request, ClaimsPrincipal user, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentSummaryDto>> GetUserLibraryAsync(DocumentQuery query, ClaimsPrincipal user, CancellationToken ct = default);
    Task<DocumentDetailDto?> GetDocumentByIdAsync(int documentId, ClaimsPrincipal user, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(int documentId, ClaimsPrincipal user, CancellationToken ct = default);
}

public interface IFileStorageService
{
    Task<StoredFileResult> StoreAsync(Stream content, string originalFileName, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default);
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}

public sealed record StoredFileResult(string StoragePath, string OriginalFileName, string ContentHash, long Length);

public interface IDocumentAuthorization
{
    bool CanView(Document document, ClaimsPrincipal user);
    bool CanDownload(Document document, ClaimsPrincipal user);
}

public interface IScanJobQueue
{
    Task EnqueueAsync(ScanJobMessage message, CancellationToken ct = default);
}

public interface IScanEngine
{
    Task<ScanVerdict> ScanAsync(Stream quarantinedContent, CancellationToken ct = default);
}

public interface IScanResultService
{
    Task<ScanApplyResult> ApplyAsync(ScanResultMessage result, CancellationToken ct = default);
}

public sealed record ScanApplyResult(bool Applied, bool Ignored, string Message);

public interface IScanJobDispatcher
{
    Task<int> DispatchPendingAsync(CancellationToken ct = default);
}

public sealed class DocumentManagementOptions
{
    public string StorageProvider { get; set; } = "Local";
    public string QueueProvider { get; set; } = "InMemory";
    public string ScannerProvider { get; set; } = "Fake";
    public string FileStorageRoot { get; set; } = "App_Data\\quarantine";
    public string ScanQueueName { get; set; } = "document-scan";
    public string ScanPoisonQueueName { get; set; } = "document-scan-poison";
    public int ScanMaxAttempts { get; set; } = 5;
    public int ScanVisibilityTimeoutSeconds { get; set; } = 30;
    public int QuarantineRetentionDays { get; set; } = 30;
    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;
    public string AzureStorageConnectionString { get; set; } = string.Empty;
    public string AzureStorageAccountUri { get; set; } = string.Empty;
    public string AzureBlobContainerName { get; set; } = "document-quarantine";
}

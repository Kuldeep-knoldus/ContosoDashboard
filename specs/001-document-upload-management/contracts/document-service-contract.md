# Document and Scan Service Contracts

These contracts are provider-neutral boundaries. Azure SDK and Function runtime
types must not cross into the Blazor pages or domain service.

## Upload and result DTOs

```csharp
public sealed record DocumentUploadRequest(
    string Title, string Category, string? Description, int? ProjectId,
    int? TaskId, string? Tags, Stream Content, string OriginalFileName,
    string ContentType, long Length);

public sealed record DocumentUploadResult(
    bool Success, int? DocumentId, string LifecycleStatus,
    string ScanStatus, string StatusMessage, string? ErrorMessage);

public sealed record ScanJobMessage(
    Guid ScanJobId, int DocumentId, int VersionNumber, string ContentHash,
    string StoragePath, string IdempotencyKey);

public sealed record ScanResultMessage(
    Guid ScanJobId, int DocumentId, int VersionNumber, string ContentHash,
    string IdempotencyKey, ScanVerdict Verdict, string ScannerVersion,
    string? ScannerCode);

public enum ScanVerdict { Clean, Flagged, TransientFailure, PermanentFailure }
```

Queue messages are schema-versioned, size-bounded, JSON-encoded envelopes.
They contain no bytes, credentials, or user tokens.

## Application interfaces

```csharp
public interface IDocumentService
{
    Task<DocumentUploadResult> UploadAsync(
        DocumentUploadRequest request, ClaimsPrincipal user,
        CancellationToken ct = default);
    Task<IReadOnlyList<DocumentSummaryDto>> GetUserLibraryAsync(
        DocumentQuery query, ClaimsPrincipal user, CancellationToken ct = default);
    Task<DocumentDetailDto?> GetDocumentByIdAsync(
        int documentId, ClaimsPrincipal user, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(
        int documentId, ClaimsPrincipal user, CancellationToken ct = default);
}

public interface IScanJobQueue
{
    Task EnqueueAsync(ScanJobMessage message, CancellationToken ct = default);
}

public interface IScanEngine
{
    Task<ScanVerdict> ScanAsync(
        Stream quarantinedContent, CancellationToken ct = default);
}

public interface IScanResultService
{
    Task ApplyAsync(ScanResultMessage result, CancellationToken ct = default);
}
```

`ApplyAsync` must validate idempotency key, current version, content hash,
document state, and authenticated scanner/result provenance before changing
visibility. Clean is the only result that can release a pending current
version. Duplicate or stale results return success-with-no-change for safe
queue acknowledgement and emit an audit/metric event.

## Function/queue behavior

- Trigger: Azure Queue Storage `ScanJobMessage`.
- On transient exception: do not acknowledge; use configured visibility timeout
  and bounded retry/dequeue policy.
- On malformed/permanent result: record the attempt and acknowledge into a
  quarantined/failed state; do not retry indefinitely.
- On poison threshold: move to poison queue, mark `ScanFailed`, retain
  uploader-only status, and alert.
- On successful application: acknowledge the message.

## Authorization contract

Every list/detail/download/preview/share operation applies the same predicate:
the caller is the uploader for non-active status, or is an authorized
owner/project member/share recipient for an active status. An ID without
permission is indistinguishable from not found. The Function has no user
authorization and may access only the private storage/object and job identified
by a valid message.

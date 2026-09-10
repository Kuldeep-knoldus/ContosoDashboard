using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services.Documents;

public sealed class ScanResultService : IScanResultService
{
    private readonly ApplicationDbContext _db;
    private readonly DocumentManagementOptions _options;
    private readonly ILogger<ScanResultService> _logger;

    public ScanResultService(ApplicationDbContext db, IOptions<DocumentManagementOptions> options, ILogger<ScanResultService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ScanApplyResult> ApplyAsync(ScanResultMessage result, CancellationToken ct = default)
    {
        var job = await _db.ScanJobs.Include(j => j.Document).SingleOrDefaultAsync(j => j.ScanJobId == result.ScanJobId, ct);
        if (job is null || job.IdempotencyKey != result.IdempotencyKey)
        {
            _logger.LogWarning("Ignoring scan result for unknown or mismatched job {ScanJobId}.", result.ScanJobId);
            return new(false, true, "Unknown or mismatched scan job.");
        }

        if (job.DocumentId != result.DocumentId || job.VersionNumber != result.VersionNumber ||
            !string.Equals(job.ContentHash, result.ContentHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Ignoring stale scan result for document {DocumentId}.", result.DocumentId);
            return new(false, true, "Stale scan result.");
        }

        if (job.Document.VersionNumber != job.VersionNumber ||
            !string.Equals(job.Document.ContentHash, job.ContentHash, StringComparison.OrdinalIgnoreCase) ||
            job.Document.LifecycleStatus is DocumentLifecycleStatus.Deleted or DocumentLifecycleStatus.Replaced)
        {
            _logger.LogWarning("Ignoring scan result for non-current document version {DocumentId}.", result.DocumentId);
            return new(false, true, "The scan result is not for the current document version.");
        }

        if (job.Status == ScanJobStatus.Completed || job.Status == ScanJobStatus.Poisoned)
        {
            return new(false, true, "Duplicate scan result.");
        }

        if (string.IsNullOrWhiteSpace(result.ScannerVersion))
        {
            return await ApplyTerminalAsync(job, ScanVerdict.Malformed, "Scanner provenance is required.", ct);
        }

        return result.Verdict switch
        {
            ScanVerdict.Clean => await ApplyTerminalAsync(job, ScanVerdict.Clean, null, ct),
            ScanVerdict.Flagged => await ApplyTerminalAsync(job, ScanVerdict.Flagged, result.ScannerCode, ct),
            ScanVerdict.PermanentFailure or ScanVerdict.Malformed => await ApplyTerminalAsync(job, result.Verdict, result.ScannerCode, ct),
            ScanVerdict.TransientFailure => await ApplyTransientAsync(job, result.ScannerCode, ct),
            _ => await ApplyTerminalAsync(job, ScanVerdict.Malformed, "Unsupported scanner verdict.", ct)
        };
    }

    private async Task<ScanApplyResult> ApplyTerminalAsync(ScanJob job, ScanVerdict verdict, string? detail, CancellationToken ct)
    {
        var document = job.Document;
        var now = DateTime.UtcNow;
        var attempt = NewAttempt(job, verdict, detail);
        _db.ScanAttempts.Add(attempt);
        job.CompletedUtc = now;
        job.Status = verdict == ScanVerdict.TransientFailure ? ScanJobStatus.Retrying : ScanJobStatus.Completed;
        document.UpdatedUtc = now;

        switch (verdict)
        {
            case ScanVerdict.Clean when document.LifecycleStatus == DocumentLifecycleStatus.PendingScan &&
                                        document.VersionNumber == job.VersionNumber &&
                                        document.ContentHash == job.ContentHash:
                document.LifecycleStatus = DocumentLifecycleStatus.Active;
                document.Visibility = DocumentVisibility.AuthorizedUsers;
                document.ScanStatus = DocumentScanStatus.Clean;
                document.ReleasedUtc = now;
                break;
            case ScanVerdict.Flagged when document.LifecycleStatus is DocumentLifecycleStatus.PendingScan or DocumentLifecycleStatus.Active:
                document.LifecycleStatus = DocumentLifecycleStatus.Quarantined;
                document.Visibility = DocumentVisibility.UploaderOnly;
                document.ScanStatus = DocumentScanStatus.Flagged;
                break;
            default:
                document.LifecycleStatus = DocumentLifecycleStatus.ScanFailed;
                document.Visibility = DocumentVisibility.UploaderOnly;
                document.ScanStatus = DocumentScanStatus.Failed;
                break;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Applied {Verdict} scan result to document {DocumentId}.", verdict, document.DocumentId);
        return new(true, false, verdict.ToString());
    }

    private async Task<ScanApplyResult> ApplyTransientAsync(ScanJob job, string? detail, CancellationToken ct)
    {
        job.Attempt++;
        if (job.Attempt >= _options.ScanMaxAttempts)
        {
            job.Status = ScanJobStatus.Poisoned;
            job.Document.LifecycleStatus = DocumentLifecycleStatus.ScanFailed;
            job.Document.Visibility = DocumentVisibility.UploaderOnly;
            job.Document.ScanStatus = DocumentScanStatus.Failed;
            job.Document.UpdatedUtc = DateTime.UtcNow;
            _db.ScanAttempts.Add(NewAttempt(job, ScanVerdict.TransientFailure, detail));
            await _db.SaveChangesAsync(ct);
            return new(true, false, "Scan moved to poison handling after maximum attempts.");
        }

        job.Status = ScanJobStatus.Retrying;
        job.LastError = detail;
        job.Document.ScanStatus = DocumentScanStatus.Processing;
        job.Document.UpdatedUtc = DateTime.UtcNow;
        _db.ScanAttempts.Add(NewAttempt(job, ScanVerdict.TransientFailure, detail));
        await _db.SaveChangesAsync(ct);
        return new(true, false, "Scan will be retried.");
    }

    private static ScanAttempt NewAttempt(ScanJob job, ScanVerdict verdict, string? detail) => new()
    {
        ScanJobId = job.ScanJobId,
        AttemptNumber = job.Attempt + 1,
        CompletedUtc = DateTime.UtcNow,
        ResultRecordedUtc = DateTime.UtcNow,
        Result = verdict switch
        {
            ScanVerdict.Clean => ScanAttemptResult.Clean,
            ScanVerdict.Flagged => ScanAttemptResult.Flagged,
            ScanVerdict.TransientFailure => ScanAttemptResult.TransientFailure,
            ScanVerdict.PermanentFailure => ScanAttemptResult.PermanentFailure,
            _ => ScanAttemptResult.Malformed
        },
        ScannerCode = detail
    };
}

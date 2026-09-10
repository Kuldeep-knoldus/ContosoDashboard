using Microsoft.EntityFrameworkCore;
using Azure;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services.Documents;

public sealed class ScanJobDispatcher : IScanJobDispatcher
{
    private readonly ApplicationDbContext _db;
    private readonly IScanJobQueue _queue;
    private readonly ILogger<ScanJobDispatcher> _logger;

    public ScanJobDispatcher(ApplicationDbContext db, IScanJobQueue queue, ILogger<ScanJobDispatcher> logger)
    {
        _db = db;
        _queue = queue;
        _logger = logger;
    }

    public async Task<int> DispatchPendingAsync(CancellationToken ct = default)
    {
        var jobs = await _db.ScanJobs.Include(j => j.Document)
            .Where(j => j.Status == ScanJobStatus.PendingDispatch || j.Status == ScanJobStatus.Retrying)
            .OrderBy(j => j.CreatedUtc).Take(50).ToListAsync(ct);
        var dispatched = 0;
        foreach (var job in jobs)
        {
            try
            {
                await _queue.EnqueueAsync(new ScanJobMessage(job.ScanJobId, job.DocumentId, job.VersionNumber,
                    job.ContentHash, job.StoragePath, job.IdempotencyKey), ct);
                job.Status = ScanJobStatus.Dispatched;
                job.DispatchedUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                dispatched++;
            }
            catch (InvalidOperationException ex)
            {
                job.Status = ScanJobStatus.Retrying;
                job.LastError = ex.Message;
                await _db.SaveChangesAsync(ct);
                _logger.LogWarning(ex, "Dispatch failed for scan job {ScanJobId}; it will be retried.", job.ScanJobId);
            }
            catch (IOException ex)
            {
                job.Status = ScanJobStatus.Retrying;
                job.LastError = ex.Message;
                await _db.SaveChangesAsync(ct);
                _logger.LogWarning(ex, "Dispatch failed for scan job {ScanJobId}; it will be retried.", job.ScanJobId);
            }
            catch (RequestFailedException ex)
            {
                job.Status = ScanJobStatus.Retrying;
                job.LastError = ex.Message;
                await _db.SaveChangesAsync(ct);
                _logger.LogWarning(ex, "Azure dispatch failed for scan job {ScanJobId}; it will be retried.", job.ScanJobId);
            }
        }

        return dispatched;
    }
}

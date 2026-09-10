namespace ContosoDashboard.Models;

public enum ScanJobStatus { PendingDispatch, Dispatched, Processing, Completed, Retrying, Poisoned, Superseded }

public sealed class ScanJob
{
    public Guid ScanJobId { get; set; } = Guid.NewGuid();
    public int DocumentId { get; set; }
    public int VersionNumber { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public int Attempt { get; set; }
    public ScanJobStatus Status { get; set; } = ScanJobStatus.PendingDispatch;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DispatchedUtc { get; set; }
    public DateTime? LeaseUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? LastError { get; set; }
    public Document Document { get; set; } = null!;
    public ICollection<ScanAttempt> Attempts { get; set; } = new List<ScanAttempt>();
}

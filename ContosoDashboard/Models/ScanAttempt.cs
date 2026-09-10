namespace ContosoDashboard.Models;

public enum ScanAttemptResult { Clean, Flagged, TransientFailure, PermanentFailure, Malformed }

public sealed class ScanAttempt
{
    public Guid ScanAttemptId { get; set; } = Guid.NewGuid();
    public Guid ScanJobId { get; set; }
    public int AttemptNumber { get; set; }
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
    public ScanAttemptResult Result { get; set; }
    public string? ScannerCode { get; set; }
    public string? ScannerVersion { get; set; }
    public DateTime? ResultRecordedUtc { get; set; }
    public string? ErrorSummary { get; set; }
    public string? ResultDigest { get; set; }
    public ScanJob ScanJob { get; set; } = null!;
}

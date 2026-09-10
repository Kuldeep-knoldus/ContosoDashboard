using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public enum DocumentLifecycleStatus { PendingScan, Active, Quarantined, ScanFailed, Deleted, Replaced }
public enum DocumentVisibility { UploaderOnly, AuthorizedUsers, Hidden }
public enum DocumentScanStatus { Pending, Processing, Clean, Flagged, Failed }

public sealed class Document
{
    [Key] public int DocumentId { get; set; }
    [Required, MaxLength(255)] public string Title { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string Category { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Description { get; set; }
    [MaxLength(1000)] public string? Tags { get; set; }
    public int UploaderId { get; set; }
    public int? ProjectId { get; set; }
    public int? TaskId { get; set; }
    [Required, MaxLength(255)] public string OriginalFileName { get; set; } = string.Empty;
    [Required, MaxLength(512)] public string StoragePath { get; set; } = string.Empty;
    [Required, MaxLength(150)] public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    [Required, MaxLength(64)] public string ContentHash { get; set; } = string.Empty;
    public int VersionNumber { get; set; } = 1;
    public DocumentLifecycleStatus LifecycleStatus { get; set; } = DocumentLifecycleStatus.PendingScan;
    public DocumentVisibility Visibility { get; set; } = DocumentVisibility.UploaderOnly;
    public DocumentScanStatus ScanStatus { get; set; } = DocumentScanStatus.Pending;
    public DateTime UploadedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedUtc { get; set; }
    public DateTime? LastAccessedUtc { get; set; }
    public User Uploader { get; set; } = null!;
    public Project? Project { get; set; }
    public ICollection<ScanJob> ScanJobs { get; set; } = new List<ScanJob>();
}

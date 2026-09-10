namespace ContosoDashboard.Services.Documents;

public static class DocumentAuditEvents
{
    public const string ScanQueued = "ScanQueued";
    public const string ScanStarted = "ScanStarted";
    public const string ScanCleanReleased = "ScanCleanReleased";
    public const string ScanFlagged = "ScanFlagged";
    public const string ScanRetry = "ScanRetry";
    public const string ScanPoisoned = "ScanPoisoned";
    public const string ScanResultIgnored = "ScanResultIgnored";
}

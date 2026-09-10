namespace ContosoDashboard.Services.Documents;

public sealed class FakeScanEngine : IScanEngine
{
    public async Task<ScanVerdict> ScanAsync(Stream quarantinedContent, CancellationToken ct = default)
    {
        using var reader = new StreamReader(quarantinedContent, leaveOpen: true);
        var contents = await reader.ReadToEndAsync(ct);
        return contents.Contains("EICAR", StringComparison.OrdinalIgnoreCase)
            ? ScanVerdict.Flagged
            : ScanVerdict.Clean;
    }
}

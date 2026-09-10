using ContosoDashboard.Models;

namespace ContosoDashboard.Services.Documents;

public sealed class ScanWorkflowService
{
    private readonly IFileStorageService _storage;
    private readonly IScanEngine _scanner;
    private readonly IScanResultService _results;
    private readonly ILogger<ScanWorkflowService> _logger;

    public ScanWorkflowService(IFileStorageService storage, IScanEngine scanner, IScanResultService results, ILogger<ScanWorkflowService> logger)
    {
        _storage = storage;
        _scanner = scanner;
        _results = results;
        _logger = logger;
    }

    public async Task<ScanApplyResult> ProcessAsync(ScanJobMessage message, CancellationToken ct = default)
    {
        await using var content = await _storage.OpenReadAsync(message.StoragePath, ct);
        var verdict = await _scanner.ScanAsync(content, ct);
        var result = new ScanResultMessage(message.ScanJobId, message.DocumentId, message.VersionNumber,
            message.ContentHash, message.IdempotencyKey, verdict, "fake-scanner-1");
        return await _results.ApplyAsync(result, ct);
    }
}

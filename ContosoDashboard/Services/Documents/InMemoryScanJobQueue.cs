using System.Collections.Concurrent;
using System.Text.Json;

namespace ContosoDashboard.Services.Documents;

public sealed class InMemoryScanJobQueue : IScanJobQueue
{
    private readonly ConcurrentQueue<ScanJobMessage> _messages = new();

    public IReadOnlyCollection<ScanJobMessage> Messages => _messages.ToArray();

    public Task EnqueueAsync(ScanJobMessage message, CancellationToken ct = default)
    {
        Validate(message);
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public bool TryDequeue(out ScanJobMessage? message) => _messages.TryDequeue(out message);

    private static void Validate(ScanJobMessage message)
    {
        if (message.SchemaVersion != 1 || message.ScanJobId == Guid.Empty ||
            message.DocumentId <= 0 || message.VersionNumber <= 0 ||
            string.IsNullOrWhiteSpace(message.ContentHash) ||
            string.IsNullOrWhiteSpace(message.StoragePath) ||
            string.IsNullOrWhiteSpace(message.IdempotencyKey))
        {
            throw new InvalidDataException("Scan job message is invalid.");
        }

        var json = JsonSerializer.Serialize(message);
        if (json.Length > 64 * 1024)
        {
            throw new InvalidDataException("Scan job message exceeds queue size limits.");
        }
    }
}

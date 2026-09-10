using System.Text.Json;
using Azure.Storage.Queues;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace ContosoDashboard.Services.Documents;

public sealed class AzureQueueScanJobQueue : IScanJobQueue
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly QueueClient _queue;

    public AzureQueueScanJobQueue(IOptions<DocumentManagementOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.AzureStorageConnectionString) &&
            string.IsNullOrWhiteSpace(options.Value.AzureStorageAccountUri))
        {
            throw new InvalidOperationException("Azure queue provider requires AzureStorageConnectionString or AzureStorageAccountUri.");
        }

        _queue = string.IsNullOrWhiteSpace(options.Value.AzureStorageConnectionString)
            ? new QueueClient(new Uri($"{options.Value.AzureStorageAccountUri.TrimEnd('/')}/{options.Value.ScanQueueName}"),
                new DefaultAzureCredential(), new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 })
            : new QueueClient(options.Value.AzureStorageConnectionString, options.Value.ScanQueueName,
                new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
    }

    public async Task EnqueueAsync(ScanJobMessage message, CancellationToken ct = default)
    {
        Validate(message);
        await _queue.CreateIfNotExistsAsync(cancellationToken: ct);
        await _queue.SendMessageAsync(JsonSerializer.Serialize(message, JsonOptions), cancellationToken: ct);
    }

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
    }
}

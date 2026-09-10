using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ContosoDashboard.Services.Documents;

namespace DocumentScanFunction;

public sealed class ScanJobFunction
{
    private readonly ScanWorkflowService _workflow;
    private readonly ILogger<ScanJobFunction> _logger;

    public ScanJobFunction(ScanWorkflowService workflow, ILogger<ScanJobFunction> logger)
    {
        _workflow = workflow;
        _logger = logger;
    }

    [Function("DocumentScanQueueTrigger")]
    public async Task RunAsync(
        [QueueTrigger("%ScanQueueName%", Connection = "AzureWebJobsStorage")] string payload,
        FunctionContext context)
    {
        var message = JsonSerializer.Deserialize<ScanJobMessage>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("The scan message payload is empty.");
        if (message.SchemaVersion != 1 || message.ScanJobId == Guid.Empty ||
            message.DocumentId <= 0 || message.VersionNumber <= 0 ||
            string.IsNullOrWhiteSpace(message.ContentHash) ||
            string.IsNullOrWhiteSpace(message.StoragePath) ||
            string.IsNullOrWhiteSpace(message.IdempotencyKey))
        {
            throw new InvalidDataException("The scan message payload is invalid.");
        }

        var result = await _workflow.ProcessAsync(message, context.CancellationToken);
        _logger.LogInformation("Scan job {ScanJobId} processed: {Message}.", message.ScanJobId, result.Message);
    }
}

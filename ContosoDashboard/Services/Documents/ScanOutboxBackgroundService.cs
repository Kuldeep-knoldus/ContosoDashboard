using Microsoft.Extensions.DependencyInjection;

namespace ContosoDashboard.Services.Documents;

public sealed class ScanOutboxBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScanOutboxBackgroundService> _logger;

    public ScanOutboxBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ScanOutboxBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = _scopeFactory.CreateScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IScanJobDispatcher>().DispatchPendingAsync(stoppingToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "The scan outbox dispatch cycle could not complete.");
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "The scan outbox storage dispatch cycle could not complete.");
            }
        }
    }
}

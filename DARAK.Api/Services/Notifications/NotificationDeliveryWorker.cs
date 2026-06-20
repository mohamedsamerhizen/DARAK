using DARAK.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace DARAK.Api.Services.Notifications;

public sealed class NotificationDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationOptions> options,
    ILogger<NotificationDeliveryWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled)
        {
            logger.LogInformation("Notification delivery worker is disabled.");
            return;
        }

        var intervalSeconds = Math.Max(5, options.Value.WorkerIntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<INotificationOutboxService>();
            var processed = await service.ProcessDueNotificationsAsync(
                Math.Max(1, options.Value.BatchSize),
                cancellationToken);

            if (processed > 0)
            {
                logger.LogInformation("Processed {ProcessedCount} pending notifications.", processed);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Notification delivery worker failed while processing notifications.");
        }
    }
}

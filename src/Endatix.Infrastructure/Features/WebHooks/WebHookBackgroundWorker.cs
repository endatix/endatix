using Endatix.Infrastructure.Features.WebHooks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// This class represents a background worker service responsible for processing WebHooks asynchronously.
#pragma warning disable S3903 // Public type left in global namespace: moving it would break NuGet consumers referencing the global name.
public class WebHookBackgroundWorker(IBackgroundTasksQueue backgroundTasksQueue, ILogger<WebHookBackgroundWorker> logger) : BackgroundService
{
    public IBackgroundTasksQueue TaskQueue => backgroundTasksQueue;

    // This method starts the background service.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogWebHookWorkerStarted(nameof(WebHookBackgroundWorker));

        await BackgroundProcessing(stoppingToken);

        logger.LogWebHookWorkerStopped(nameof(WebHookBackgroundWorker));
    }

    private async Task BackgroundProcessing(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await backgroundTasksQueue.DequeueAsync(stoppingToken);

            try
            {
                await workItem(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error occurred executing {WorkItem}.", nameof(workItem));
            }

            // Optional: Preventing tight loops by giving 200 ms delay
            await Task.Delay(200, stoppingToken);
        }
    }
}
#pragma warning restore S3903

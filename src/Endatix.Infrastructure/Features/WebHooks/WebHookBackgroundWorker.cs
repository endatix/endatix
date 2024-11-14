using Endatix.Infrastructure.Features.WebHooks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// This class represents a background worker service responsible for processing WebHooks asynchronously.
public class WebHookBackgroundWorker(IBackgroundTasksQueue backgroundTasksQueue, ILogger<WebHookBackgroundWorker> logger) : BackgroundService
{
    public IBackgroundTasksQueue TaskQueue => backgroundTasksQueue;

    // This method starts the background service.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("{service} Service is running.", nameof(WebHookBackgroundWorker));

        await BackgroundProcessing(stoppingToken);

        logger.LogInformation("{service} Service is stopped.", nameof(WebHookBackgroundWorker));
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
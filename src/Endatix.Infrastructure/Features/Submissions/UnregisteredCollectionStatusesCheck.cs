using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// Logs, once at startup and at Warning, each stored collection status code that is not registered. Such a code still
/// loads and reads as closed, so its submissions can't resume; the log says where a removed configuration line left them.
/// Runs in the background, so a large table doesn't hold up startup.
/// </summary>
internal sealed partial class UnregisteredCollectionStatusesCheck(
    IServiceScopeFactory scopeFactory,
    ILogger<UnregisteredCollectionStatusesCheck> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            foreach (var code in await UnregisteredCodesAsync(stoppingToken))
            {
                LogUnregistered(code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCheckFailed(ex);
        }
    }

    private async Task<IReadOnlyList<string>> UnregisteredCodesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<AppDbContext>();
        if (dbContext is null)
        {
            return [];
        }

        var registered = CollectionStatusCatalog.Registered.Keys.ToArray();
        var stored = await dbContext.Submissions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(submission => !submission.IsDeleted)
            .Select(submission => submission.CollectionStatus)
            .Distinct()
            .ToListAsync(cancellationToken);
        return [.. stored.Select(status => status.Code).Where(code => !registered.Contains(code)).Order(StringComparer.Ordinal)];
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Collection status '{Code}' is stored but not registered in Endatix:Submissions:CollectionStatuses; its submissions read as closed.")]
    private partial void LogUnregistered(string code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not check stored collection statuses at startup.")]
    private partial void LogCheckFailed(Exception exception);
}

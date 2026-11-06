using Endatix.Core.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// Gives <see cref="CollectionStatusCatalog"/> the host's logger when the host starts, so a stored code nobody
/// registered is logged once, the first time it is read.
/// </summary>
internal sealed class CollectionStatusCatalogLogging(ILoggerFactory loggerFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        CollectionStatusCatalog.UseLogger(loggerFactory.CreateLogger(typeof(CollectionStatusCatalog)));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

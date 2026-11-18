using Endatix.Core.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>Logs one warning at startup when the enforced policy allows private and local network targets.</summary>
internal sealed class PrivateNetworkTargetsStartupWarning(
    OutboundAddressPolicy policy,
    ILogger<PrivateNetworkTargetsStartupWarning> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (policy.AllowPrivateNetworkTargets)
        {
            logger.LogPrivateNetworkTargetsAllowed();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

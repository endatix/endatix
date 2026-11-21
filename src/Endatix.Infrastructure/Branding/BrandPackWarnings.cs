using Endatix.Infrastructure.Email;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Branding;

/// <summary>
/// Logs, once at startup, each brand pack field the API could not use and replaced with its built-in value. The pack
/// is read while the host is built, before logging exists, so the lines are kept until the host starts.
/// </summary>
internal sealed class BrandPackWarnings(IReadOnlyList<string> warnings, ILogger<BrandPackWarnings> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var warning in warnings)
        {
            logger.LogBrandPackFieldIgnored(warning);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

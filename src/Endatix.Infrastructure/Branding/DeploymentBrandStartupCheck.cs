using Endatix.Core.Branding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Endatix.Infrastructure.Branding;

/// <summary>
/// Resolves the <see cref="DeploymentBrand"/> when the host starts. A host that registers the brand lazily (an email
/// sender without <c>AddDeploymentBrand</c>) then reads the pack at startup, and an invalid pack stops the host with
/// <see cref="BrandPackException"/> instead of failing every email.
/// </summary>
internal sealed class DeploymentBrandStartupCheck(IServiceProvider services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        services.GetRequiredService<DeploymentBrand>();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

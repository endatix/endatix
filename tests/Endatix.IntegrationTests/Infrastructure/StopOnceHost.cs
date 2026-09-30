using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Endatix.IntegrationTests;

/// <summary>
/// A test host that stops once, from the application's own <c>app.Run()</c>, instead of twice at once.
/// </summary>
/// <remarks>
/// <c>WebApplicationFactory</c> stops its host on dispose. That raises <c>ApplicationStopping</c>, which returns the
/// application's <c>app.Run()</c>, which stops the same host again on another thread
/// (https://github.com/dotnet/aspnetcore/issues/40271). Every hosted service is then stopped twice, possibly
/// concurrently. Stopping this host only asks the application to stop and waits until it has, so the one stop is
/// <c>app.Run()</c>'s.
/// </remarks>
internal sealed class StopOnceHost : IHost
{
    private static readonly TimeSpan StopMargin = TimeSpan.FromSeconds(30);

    private readonly IHost _inner;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly TimeSpan _stopBound;

    public StopOnceHost(IHost inner)
    {
        _inner = inner;

        // Read while the host runs: app.Run() disposes the container once it has stopped.
        _lifetime = inner.Services.GetRequiredService<IHostApplicationLifetime>();
        _stopBound = inner.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout + StopMargin;
    }

    public IServiceProvider Services => _inner.Services;

    public Task StartAsync(CancellationToken cancellationToken = default) => _inner.StartAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = _lifetime.ApplicationStopped.Register(() => stopped.TrySetResult());

        _lifetime.StopApplication();
        await stopped.Task.WaitAsync(_stopBound, cancellationToken);
    }

    public void Dispose() => _inner.Dispose();
}

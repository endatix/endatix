using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Endatix.IntegrationTests;

/// <summary>
/// A test host that stops once, from the application's own <c>app.Run()</c>, instead of twice at once, and throws
/// what that stop threw.
/// </summary>
/// <remarks>
/// <para>
/// <c>WebApplicationFactory</c> stops its host on dispose. That raises <c>ApplicationStopping</c>, which returns the
/// application's <c>app.Run()</c>, which stops the same host again on another thread
/// (https://github.com/dotnet/aspnetcore/issues/40271). Every hosted service is then stopped twice, possibly
/// concurrently. Stopping this host only asks the application to stop and waits until it has, so the one stop is
/// <c>app.Run()</c>'s.
/// </para>
/// <para>
/// That stop throws a hosted service's failure to <c>app.Run()</c>, and from there to the entry point, which
/// <c>WebApplicationFactory</c> no longer watches once the host has started. <c>ApplicationStopped</c> does not
/// carry it either: the host raises it before it throws. So this host waits for <see cref="HostStopOutcome"/>, the
/// message the host logs as its stop's last step, and throws the failure that message carries.
/// </para>
/// <para>
/// If a .NET release renumbers those events, renames the host's logging category or logs them below Debug, no stop
/// ever ends here: every teardown fails with a timeout, rather than passing without seeing the stop.
/// </para>
/// </remarks>
internal sealed class StopOnceHost : IHost
{
    private static readonly TimeSpan StopMargin = TimeSpan.FromSeconds(30);

    private readonly IHost _inner;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly HostStopOutcome _outcome;
    private readonly TimeSpan _stopBound;
    private readonly Lock _stopGate = new();
    private Task? _stopped;

    public StopOnceHost(IHost inner)
    {
        _inner = inner;

        // Read while the host runs: app.Run() disposes the container once it has stopped.
        _lifetime = inner.Services.GetRequiredService<IHostApplicationLifetime>();
        _outcome = inner.Services.GetRequiredService<HostStopOutcome>();
        _stopBound = inner.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout + StopMargin;
    }

    public IServiceProvider Services => _inner.Services;

    public Task StartAsync(CancellationToken cancellationToken = default) => _inner.StartAsync(cancellationToken);

    /// <summary>Stops the host and waits for its stop to end; the first call throws the stop's failure.</summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _lifetime.StopApplication();
        if (StartsWaiting(cancellationToken, out var stopped))
        {
            await stopped;
            return;
        }

        // WebApplicationFactory stops its host again on every dispose that did not finish, and a root factory disposes
        // the factories derived from it. Throwing each time would replace the test's own failure with this one, and
        // a fresh wait would hold each of those disposals for another full bound after a stop that timed out.
        await stopped.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    /// <summary>The one wait for the stop's outcome, and whether this call started it.</summary>
    private bool StartsWaiting(CancellationToken cancellationToken, out Task stopped)
    {
        lock (_stopGate)
        {
            var first = _stopped is null;
            _stopped ??= _outcome.Stopped.WaitAsync(_stopBound, cancellationToken);
            stopped = _stopped;
            return first;
        }
    }

    public void Dispose() => _inner.Dispose();
}

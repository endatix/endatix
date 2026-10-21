namespace Endatix.IntegrationTests.Infrastructure;

/// <summary>
/// Requests a test started alongside a pause. Disposing waits for each, so none runs on into the next test; declare
/// it before the <see cref="CommandPause"/>, which is then disposed first and lets the paused request finish.
/// </summary>
public sealed class StartedRequests : IAsyncDisposable
{
    private readonly List<Task> _started = [];

    public Task<T> Add<T>(Task<T> request)
    {
        _started.Add(request);
        return request;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var request in _started)
        {
            await request.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
}

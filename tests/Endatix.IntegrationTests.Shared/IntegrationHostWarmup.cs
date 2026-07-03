namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Ensures the test host is ready by triggering the first HTTP request so hosted services start.
/// </summary>
public static class IntegrationHostWarmup
{
    /// <summary>
    /// Starts the host pipeline (including <c>DatabaseMigrationService</c>) via an initial HTTP request.
    /// </summary>
    public static Task EnsureReadyAsync(
        IServiceProvider services,
        Func<HttpClient> createClient,
        CancellationToken cancellationToken = default)
    {
        _ = services;
        _ = cancellationToken;

        using var client = createClient();
        return Task.CompletedTask;
    }
}

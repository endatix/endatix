using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Ensures the test host is ready by running EF Core migrations.
/// </summary>
public static class IntegrationHostWarmup
{
    /// <summary>
    /// Runs EF Core migrations for <see cref="AppDbContext"/> and <see cref="AppIdentityDbContext"/>.
    /// </summary>
    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        Func<HttpClient> createClient,
        CancellationToken cancellationToken = default)
    {
        using var client = createClient();

        await using var scope = services.CreateAsyncScope();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        await appDb.Database.MigrateAsync(cancellationToken);
        await identityDb.Database.MigrateAsync(cancellationToken);
    }
}

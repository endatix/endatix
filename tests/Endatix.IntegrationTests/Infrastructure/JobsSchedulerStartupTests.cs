using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.IntegrationTests;

/// <summary>
/// How a jobs node starts when its database is not there: the host comes up and the scheduler waits for the database,
/// on either provider.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class JobsSchedulerStartupTests(DbIntegrationFixture fixture)
{
    private const string DeferredStartError =
        "The background jobs scheduler could not reach its database and will start once it can; until then this host runs no jobs.";

    [Fact]
    public async Task Scheduler_with_unreachable_database_defers_start()
    {
        // Arrange — nothing listens on port 1, and the short timeout keeps each try well inside the wait below.
        var ct = TestContext.Current.CancellationToken;
        var logs = new CapturingLoggerProvider();
        await using var node = JobsTestNode.Create(
            ClosedPort(fixture.Provider),
            configureServices: services => services.AddSingleton<ILoggerProvider>(logs),
            migrate: false);

        // Act
        await node.StartAsync(ct);
        await Task.Delay(TimeSpan.FromSeconds(10), ct);

        // Assert
        logs.Entries.Should().Contain((LogLevel.Error, DeferredStartError));
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    private static JobsTestNodeDatabase ClosedPort(TestDatabaseProvider provider) => provider switch
    {
        TestDatabaseProvider.PostgreSql => new(
            provider, "Host=127.0.0.1;Port=1;Database=endatix;Username=endatix;Password=endatix;Timeout=1"),
        TestDatabaseProvider.SqlServer => new(
            provider,
            "Server=127.0.0.1,1;Database=endatix;User ID=endatix;Password=endatix;Connect Timeout=1;TrustServerCertificate=True"),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider."),
    };
}

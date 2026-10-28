extern alias EndatixWebHost;

using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// A full web host on a database of its own, so its outbox relay is the only one claiming that database's
/// messages. The modules' inline outbox handlers stay registered, so the startup check sees every one of them, unless
/// the test strips them to deliver inline to its own.
/// </summary>
internal sealed class FanOutHost : IAsyncDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly WebApplicationFactory<EndatixWebHost::Program> _factory;

    private FanOutHost(JobsTestDatabase database, WebApplicationFactory<EndatixWebHost::Program> factory, CapturingLoggerProvider logs)
    {
        Database = database;
        _factory = factory;
        Logs = logs;
    }

    public JobsTestDatabase Database { get; }

    public CapturingLoggerProvider Logs { get; }

    public IServiceProvider Services => _factory.Services;

    /// <summary>Starts a host on a new database of the fixture's provider, on the fixture's server.</summary>
    public static async Task<FanOutHost> StartAsync(
        DbIntegrationFixture fixture,
        FanOutHostSetup setup,
        CancellationToken cancellationToken)
    {
        var database = await JobsTestDatabase.CreateAsync(fixture, cancellationToken);
        var logs = new CapturingLoggerProvider();
        var factory = new EndatixWebApplicationFactory(database.ConnectionString, database.Provider)
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Endatix:Outbox:DeliverToJobQueue", setup.DeliverToJobQueue.ToString());
                builder.UseSetting("Endatix:Outbox:PollIntervalSeconds", "1");
                builder.UseSetting("Endatix:FeatureFlags:JobsModule", setup.JobsModule.ToString());
                builder.UseSetting("Endatix:BackgroundJobs:IdleWaitTimeSeconds", "1");
                foreach (var (key, value) in setup.Settings)
                {
                    builder.UseSetting(key, value);
                }

                builder.ConfigureTestServices(services =>
                {
                    if (setup.StripsInlineHandlers)
                    {
                        services.RemoveAll<IOutboxIntegrationEventHandler>();
                    }

                    services.AddSingleton<ILoggerProvider>(logs);
                    setup.ConfigureServices(services);
                });
            });

        // Reading the services builds and starts the host, and with it the migrations. The relay and the scheduler are
        // then switched on: this host is the only one on its database, and they are what its tests exercise.
        _ = factory.Services;
        await BackgroundWork.Of(factory.Services).StartAsync(cancellationToken);
        return new FanOutHost(database, factory, logs);
    }

    // The time is bound in UTC, which the columns hold, because a server clock function returns the server's local
    // time on SQL Server. A quoted boolean literal reads the same on both providers.
    public Task InsertMessageAsync(OutboxRow message, CancellationToken cancellationToken) =>
        Database.ExecuteAsync(
            $"""
             INSERT INTO "OutboxMessages" ("Id", "EventType", "Payload", "TenantId", "OccurredAt", "SchemaVersion", "Status", "Attempts", "CreatedAt", "IsDeleted")
             VALUES ({message.Id}, '{message.EventType}', '{message.Payload}', {message.TenantId}, @now, 1, 0, 0, @now, 'false')
             """,
            new Dictionary<string, object> { ["@now"] = DateTime.UtcNow },
            cancellationToken);

    /// <summary>Waits until the message's row reaches the state <paramref name="reached"/> accepts.</summary>
    public Task<bool> WaitForMessageAsync(
        long id,
        Func<(int Status, int Attempts, string? LockedBy), bool> reached,
        CancellationToken cancellationToken) =>
        JobsTestWait.UntilAsync(async () => reached(await ReadMessageAsync(id, cancellationToken)), Patience, cancellationToken);

    public async Task<(int Status, int Attempts, string? LockedBy)> ReadMessageAsync(long id, CancellationToken cancellationToken) =>
        (await Database.QueryAsync(
            $"""SELECT "Status", "Attempts", "LockedBy" FROM "OutboxMessages" WHERE "Id" = {id}""",
            reader => (reader.GetInt32(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetString(2)),
            cancellationToken)).Single();

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await Database.DisposeAsync();
    }
}

/// <summary>How a <see cref="FanOutHost"/> delivers outbox messages, and what each test adds to its services.</summary>
/// <param name="DeliverToJobQueue">The value of <c>Endatix:Outbox:DeliverToJobQueue</c>.</param>
/// <param name="ConfigureServices">Registers the test's subscriptions and handlers.</param>
internal sealed record FanOutHostSetup(bool DeliverToJobQueue, Action<IServiceCollection> ConfigureServices)
{
    /// <summary>Whether the Jobs module is on.</summary>
    public bool JobsModule { get; init; } = true;

    /// <summary>
    /// Whether the modules' inline outbox handlers are removed, for a test that delivers inline to handlers of its own.
    /// Kept by default, so the startup check that every one of them has a job subscription sees them.
    /// </summary>
    public bool StripsInlineHandlers { get; init; }

    /// <summary>Further configuration the test sets on the host.</summary>
    public IReadOnlyDictionary<string, string?> Settings { get; init; } = new Dictionary<string, string?>();
}

/// <summary>An outbox message row a test inserts for the relay to claim.</summary>
internal sealed record OutboxRow(long Id, string EventType, long TenantId, string Payload = "{}");

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
/// messages. The inline outbox handlers are replaced by what each test registers.
/// </summary>
internal sealed class FanOutHost : IAsyncDisposable
{
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

    public static async Task<FanOutHost> StartAsync(
        string serverConnectionString,
        bool deliverToJobQueue,
        Action<IServiceCollection> configureServices,
        CancellationToken cancellationToken,
        bool jobsModule = true)
    {
        var database = await JobsTestDatabase.CreateAsync(serverConnectionString, cancellationToken);
        var logs = new CapturingLoggerProvider();
        var factory = new EndatixWebApplicationFactory(database.ConnectionString, TestDatabaseProvider.PostgreSql)
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Endatix:Outbox:DeliverToJobQueue", deliverToJobQueue.ToString());
                builder.UseSetting("Endatix:Outbox:PollIntervalSeconds", "1");
                builder.UseSetting("Endatix:FeatureFlags:JobsModule", jobsModule.ToString());
                builder.UseSetting("Endatix:BackgroundJobs:IdleWaitTimeSeconds", "1");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOutboxIntegrationEventHandler>();
                    services.AddSingleton<ILoggerProvider>(logs);
                    configureServices(services);
                });
            });

        // Reading the services builds and starts the host, and with it the migrations and the relay.
        _ = factory.Services;
        return new FanOutHost(database, factory, logs);
    }

    public Task InsertMessageAsync(long id, string eventType, long tenantId, CancellationToken cancellationToken, string payload = "{}") =>
        Database.ExecuteAsync(
            $"""
             INSERT INTO "OutboxMessages" ("Id", "EventType", "Payload", "TenantId", "OccurredAt", "SchemaVersion", "Status", "Attempts", "CreatedAt", "IsDeleted")
             VALUES ({id}, '{eventType}', '{payload}', {tenantId}, now(), 1, 0, 0, now(), false)
             """,
            cancellationToken);

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

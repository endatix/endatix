using System.Net;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.WebHooks;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Webhooks delivered as jobs, one per endpoint, to a stub receiver: each endpoint retries and dead-letters on
/// its own.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class WebHookJobDeliveryTests(DbIntegrationFixture fixture)
{
    private const string SkipReason =
        "Background jobs are PostgreSQL-only; the module is not registered on this provider.";

    private const long MissingFormId = 424242;
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Each_endpoint_gets_its_own_job()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var receiver = new StubWebHookReceiver();
        await using var host = await StartHostAsync(ct);
        var tenantId = await SeedTenantAsync(host, new TenantSeed("webhook-endpoints", Config(receiver.UrlFor("a"), receiver.UrlFor("b"), receiver.UrlFor("c"))), ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(500, "submission.completed", tenantId, SubmissionPayload(tenantId, MissingFormId)), ct);
        await WaitForSentAsync(host, 500, ct);

        // Assert
        var jobs = await WebHookJobsAsync(host, ct);
        jobs.Should().HaveCount(3);
        jobs.Should().OnlyContain(job => !job.PayloadJson.Contains("http") && !job.DedupKey.Contains("http"));
        foreach (var job in jobs)
        {
            using var payload = JsonDocument.Parse(job.PayloadJson);
            payload.RootElement.EnumerateObject().Select(property => property.Name)
                .Should().BeEquivalentTo("outboxMessageId", "endpointKey");
        }
    }

    [Fact]
    public async Task Dead_endpoint_does_not_affect_healthy_ones()
    {
        // Arrange — endpoint B always answers 503; retries are one second apart to keep the test short.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var receiver = new StubWebHookReceiver();
        await using var host = await StartHostAsync(ct, new Dictionary<string, string?>
        {
            ["Endatix:BackgroundJobs:JobTypes:WebHookDelivery:BackoffBaseSeconds"] = "1",
            ["Endatix:BackgroundJobs:JobTypes:WebHookDelivery:BackoffCapSeconds"] = "1",
        });
        var urlA = receiver.UrlFor("a");
        var urlB = receiver.UrlFor("b", HttpStatusCode.ServiceUnavailable);
        var urlC = receiver.UrlFor("c");
        var tenantId = await SeedTenantAsync(host, new TenantSeed("webhook-dead-endpoint", Config(urlA, urlB, urlC)), ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(501, "submission.completed", tenantId, SubmissionPayload(tenantId, MissingFormId)), ct);
        var deadLettered = await JobsTestWait.UntilAsync(
            async () => (await WebHookJobsAsync(host, ct)).Any(job => job.Status == 5),
            Patience,
            ct);
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        // Assert
        deadLettered.Should().BeTrue();
        var jobs = (await WebHookJobsAsync(host, ct)).ToDictionary(job => job.DedupKey.Split(':')[1]);
        jobs[WebHookEndpointKey.Of(urlA)].Should().Match<WebHookJob>(job => job.Status == 3);
        jobs[WebHookEndpointKey.Of(urlC)].Should().Match<WebHookJob>(job => job.Status == 3);
        jobs[WebHookEndpointKey.Of(urlB)].Should().Match<WebHookJob>(job => job.Status == 5 && job.AttemptCount == 8);
        receiver.CountFor("a").Should().Be(1);
        receiver.CountFor("c").Should().Be(1);
        receiver.CountFor("b").Should().Be(8);
        receiver.Received.Should().OnlyContain(request => request.HookId == "501");
    }

    [Fact]
    public async Task Disabled_event_creates_no_webhook_jobs()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var receiver = new StubWebHookReceiver();
        await using var host = await StartHostAsync(ct);
        var tenantId = await SeedTenantAsync(host, new TenantSeed("webhook-disabled", Config([receiver.UrlFor("a")], isEnabled: false)), ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(502, "submission.completed", tenantId, SubmissionPayload(tenantId, MissingFormId)), ct);
        await WaitForSentAsync(host, 502, ct);

        // Assert
        (await WebHookJobsAsync(host, ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Form_config_overrides_tenant_config()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var receiver = new StubWebHookReceiver();
        await using var host = await StartHostAsync(ct);
        var tenantUrl = receiver.UrlFor("tenant");
        var formUrl = receiver.UrlFor("form");
        var tenantId = await SeedTenantAsync(host, new TenantSeed("webhook-form-config", Config(tenantUrl)), ct);
        var formId = await SeedFormAsync(host, new FormSeed(tenantId, Config(formUrl)), ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(503, "submission.completed", tenantId, SubmissionPayload(tenantId, formId)), ct);
        await WaitForSentAsync(host, 503, ct);

        // Assert
        var jobs = await WebHookJobsAsync(host, ct);
        jobs.Should().ContainSingle().Which.DedupKey.Should().Be($"503:{WebHookEndpointKey.Of(formUrl)}");
    }

    private Task<FanOutHost> StartHostAsync(CancellationToken ct, IReadOnlyDictionary<string, string?>? settings = null) =>
        FanOutHost.StartAsync(
            fixture.ConnectionString,
            new FanOutHostSetup(DeliverToJobQueue: true, _ => { }) { Settings = settings ?? new Dictionary<string, string?>() },
            ct);

    private static async Task<long> SeedTenantAsync(FanOutHost host, TenantSeed seed, CancellationToken ct)
    {
        var tenantId = await new IntegrationSeedBuilder(host.Services).SeedTenantAsync(seed.Name, cancellationToken: ct);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.Set<TenantSettings>().IgnoreQueryFilters().FirstOrDefaultAsync(row => row.TenantId == tenantId, ct);
        if (settings is null)
        {
            db.Set<TenantSettings>().Add(new TenantSettings(tenantId, webHookSettingsJson: seed.WebHookConfig));
        }
        else
        {
            settings.UpdateWebHookSettings(JsonSerializer.Deserialize<WebHookConfiguration>(seed.WebHookConfig)!);
        }

        await db.SaveChangesAsync(ct);
        return tenantId;
    }

    private static async Task<long> SeedFormAsync(FanOutHost host, FormSeed seed, CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var form = Form.Create(new FormCreateArgs(TenantId: seed.TenantId, Name: "webhook form", WebHookSettingsJson: seed.WebHookConfig));
        db.Forms.Add(form);
        await db.SaveChangesAsync(ct);
        return form.Id;
    }

    private static string Config(params string[] urls) => Config(urls, isEnabled: true);

    private static string Config(string[] urls, bool isEnabled) =>
        JsonSerializer.Serialize(new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                ["SubmissionCompleted"] = new()
                {
                    IsEnabled = isEnabled,
                    WebHookEndpoints = [.. urls.Select(url => new WebHookEndpointConfig { Url = url })],
                },
            },
        });

    private static string SubmissionPayload(long tenantId, long formId) =>
        $$"""{"formId":"{{formId}}","submissionId":"77","tenantId":"{{tenantId}}"}""";

    private static Task WaitForSentAsync(FanOutHost host, long messageId, CancellationToken ct) =>
        JobsTestWait.UntilAsync(async () => (await host.ReadMessageAsync(messageId, ct)).Status == 1, Patience, ct);

    private static Task<List<WebHookJob>> WebHookJobsAsync(FanOutHost host, CancellationToken ct) =>
        host.Database.QueryAsync(
            """SELECT "PayloadJson"::text, "DedupKey", "Status", "AttemptCount" FROM jobs."BackgroundJobs" WHERE "JobType" = 'WebHookDelivery'""",
            reader => new WebHookJob(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3)),
            ct);

    private sealed record TenantSeed(string Name, string WebHookConfig);

    private sealed record FormSeed(long TenantId, string WebHookConfig);

    private sealed record WebHookJob(string PayloadJson, string DedupKey, int Status, int AttemptCount);
}

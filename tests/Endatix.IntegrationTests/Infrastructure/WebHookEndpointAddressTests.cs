using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.BackgroundJobs.Handlers;
using Endatix.Infrastructure.Features.WebHooks;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Webhook endpoints on loopback, sent to in both delivery modes: refused once and never retried while private network
/// targets are not allowed, delivered when they are, and a redirect is never followed.
/// </summary>
[Collection(nameof(JobsDbTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("Lane", "Jobs")]
public sealed class WebHookEndpointAddressTests(DbIntegrationFixture fixture)
{
    private const long MissingFormId = 525252;
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Inline_delivery_to_a_loopback_endpoint_sends_nothing_and_settles_the_row()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var receiver = new StubWebHookReceiver();
        await using var host = await StartHostAsync(deliverToJobQueue: false, settings: null, ct);
        var url = receiver.UrlFor("inline-refused");
        var tenantId = await SeedTenantAsync(host, new TenantSeed("webhook-inline-refused", Config(url)), ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(700, "submission.completed", tenantId, SubmissionPayload(tenantId)), ct);
        var settled = await host.WaitForMessageAsync(700, row => row.Status == 1, ct);
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        // Assert
        settled.Should().BeTrue();
        (await host.ReadMessageAsync(700, ct)).Status.Should().Be(1);
        receiver.Received.Should().BeEmpty();
        var refusals = host.Logs.Entries.Where(entry => entry.Message.Contains("was refused (NonPublicAddress)")).ToList();
        refusals.Should().ContainSingle().Which.Message.Should().Contain(WebHookEndpointKey.Of(url));
        host.Logs.Entries.Should().NotContain(entry => entry.Message.Contains(receiver.BaseUrl) || entry.Message.Contains("127.0.0.1"));
    }

    [Fact]
    public async Task Job_delivery_to_a_loopback_endpoint_fails_the_job_after_one_attempt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var receiver = new StubWebHookReceiver();
        await using var host = await StartHostAsync(deliverToJobQueue: true, new Dictionary<string, string?>
        {
            ["Endatix:BackgroundJobs:JobTypes:WebHookDelivery:BackoffBaseSeconds"] = "1",
            ["Endatix:BackgroundJobs:JobTypes:WebHookDelivery:BackoffCapSeconds"] = "1",
        }, ct);
        var tenantId = await SeedTenantAsync(host, new TenantSeed("webhook-job-refused", Config(receiver.UrlFor("job-refused"))), ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(701, "submission.completed", tenantId, SubmissionPayload(tenantId)), ct);
        var ended = await JobsTestWait.UntilAsync(
            async () => (await WebHookJobsAsync(host, ct)).Any(job => job.Status >= 3), Patience, ct);
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        // Assert — Failed (4), not retried, and the reason names no address.
        ended.Should().BeTrue();
        var job = (await WebHookJobsAsync(host, ct)).Should().ContainSingle().Subject;
        job.Status.Should().Be(4);
        job.AttemptCount.Should().Be(1);
        job.ErrorMessage.Should().Contain(WebHookDeliveryJobHandler.EndpointAddressNotAllowed);
        receiver.Received.Should().BeEmpty();
    }

    [Fact]
    public async Task Redirect_from_an_endpoint_is_not_followed()
    {
        // Arrange — private targets allowed, so the first request reaches the receiver; one attempt per job.
        var ct = TestContext.Current.CancellationToken;
        await using var receiver = new StubWebHookReceiver();
        await using var host = await StartHostAsync(deliverToJobQueue: true, new Dictionary<string, string?>
        {
            [StubWebHookReceiver.AllowPrivateNetworkTargetsSetting] = "true",
            ["Endatix:BackgroundJobs:JobTypes:WebHookDelivery:MaxAttempts"] = "1",
        }, ct);
        var tenantId = await SeedTenantAsync(host, new TenantSeed("webhook-redirect", Config(receiver.RedirectFor("hop", "target"))), ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(702, "submission.completed", tenantId, SubmissionPayload(tenantId)), ct);
        var ended = await JobsTestWait.UntilAsync(
            async () => (await WebHookJobsAsync(host, ct)).Any(job => job.Status >= 3), Patience, ct);

        // Assert — the 302 is a failed delivery, and the target never gets a request.
        ended.Should().BeTrue();
        (await WebHookJobsAsync(host, ct)).Should().ContainSingle().Which.Status.Should().NotBe(3);
        receiver.CountFor("hop").Should().Be(1);
        receiver.CountFor("target").Should().Be(0);
    }

    [Fact]
    public async Task Inline_delivery_to_a_loopback_endpoint_is_sent_when_private_targets_are_allowed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var receiver = new StubWebHookReceiver();
        await using var host = await StartHostAsync(deliverToJobQueue: false, new Dictionary<string, string?>
        {
            [StubWebHookReceiver.AllowPrivateNetworkTargetsSetting] = "true",
        }, ct);
        var tenantId = await SeedTenantAsync(host, new TenantSeed("webhook-inline-allowed", Config(receiver.UrlFor("inline-allowed"))), ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(703, "submission.completed", tenantId, SubmissionPayload(tenantId)), ct);
        var sent = await host.WaitForMessageAsync(703, row => row.Status == 1, ct);

        // Assert
        sent.Should().BeTrue();
        receiver.CountFor("inline-allowed").Should().Be(1);
    }

    private Task<FanOutHost> StartHostAsync(
        bool deliverToJobQueue,
        IReadOnlyDictionary<string, string?>? settings,
        CancellationToken ct) =>
        FanOutHost.StartAsync(
            fixture,
            new FanOutHostSetup(deliverToJobQueue, _ => { }) { Settings = settings ?? new Dictionary<string, string?>() },
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

    private static string Config(string url) =>
        JsonSerializer.Serialize(new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                ["SubmissionCompleted"] = new()
                {
                    IsEnabled = true,
                    WebHookEndpoints = [new WebHookEndpointConfig { Url = url }],
                },
            },
        });

    private static string SubmissionPayload(long tenantId) =>
        $$"""{"formId":"{{MissingFormId}}","submissionId":"77","tenantId":"{{tenantId}}"}""";

    private static Task<List<WebHookJob>> WebHookJobsAsync(FanOutHost host, CancellationToken ct) =>
        host.Database.QueryAsync(
            """SELECT "Status", "AttemptCount", "ErrorMessage" FROM jobs."BackgroundJobs" WHERE "JobType" = 'WebHookDelivery'""",
            reader => new WebHookJob(reader.GetInt32(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetString(2)),
            ct);

    private sealed record TenantSeed(string Name, string WebHookConfig);

    private sealed record WebHookJob(int Status, int AttemptCount, string? ErrorMessage);
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Same_token_loses_and_regains_access_at_the_report_deadlines()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var clock = new ReportClock();
        var start = clock.Now;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IDateTimeProvider>(clock);
            services.AddSingleton<IRequiredActionProvider>(new ScheduledCheck(clock));
            services.AddRequiredActionEnforcement();
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);

        // Act / Assert: only time moves; no invalidation or new credentials.
        var clear = await CheckAccessAsync(client, HttpStatusCode.OK, ct);
        clock.Now = start.AddSeconds(5).AddTicks(-1);
        Assert.Equal(clear, await CheckAccessAsync(client, HttpStatusCode.OK, ct));
        clock.Now = start.AddSeconds(5);
        var blocked = await CheckAccessAsync(client, HttpStatusCode.Forbidden, ct);
        Assert.NotEqual(clear, blocked);
        clock.Now = start.AddSeconds(9);
        Assert.Equal(clear, await CheckAccessAsync(client, HttpStatusCode.OK, ct));
    }

    private static async Task<string> CheckAccessAsync(HttpClient client, HttpStatusCode expected, CancellationToken ct)
    {
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        using var operation = await client.GetAsync("/api/system/version", ct);
        Assert.Equal(expected, operation.StatusCode);
        Assert.Equal(expected == HttpStatusCode.OK ? 0 : 1, me.GetProperty("requiredActions").GetArrayLength());
        return me.GetProperty("eTag").GetString()!;
    }

    private sealed class ReportClock : IDateTimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed class ScheduledCheck(ReportClock clock) : IRequiredActionProvider
    {
        private readonly DateTimeOffset _starts = clock.Now.AddSeconds(5);
        private readonly DateTimeOffset _ends = clock.Now.AddSeconds(9);
        public string CacheConfigurationKey => "scheduled-check";
        public bool FailClosed => true;

        public Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken)
        {
            IReadOnlyList<RequiredAction> result = clock.Now < _starts ? new RequiredActionReport([], _starts)
                : clock.Now < _ends ? new RequiredActionReport([new RequiredAction
                {
                    Key = "profile", ResolvePath = "/profile", Scope = RequiredActionScope.User, Severity = RequiredActionSeverity.Blocking
                }], _ends) : [];
            return Task.FromResult(result);
        }
    }
}

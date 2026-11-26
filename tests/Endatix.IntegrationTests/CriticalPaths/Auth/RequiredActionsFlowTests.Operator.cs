using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Operator_operations_return_the_same_actions_as_me()
    {
        var ct = TestContext.Current.CancellationToken;
        var mail = Substitute.For<IEmailSender>();
        var storage = Substitute.For<IStorageStatsRepository>();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton(mail);
            services.AddSingleton(storage);
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("legal", "/terms"));
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var admin = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        Assert.Single(me.GetProperty("requiredActions").EnumerateArray());
        var before = await OperatorStateAsync(world, ct);
        mail.ClearReceivedCalls();

        foreach (var operation in OperatorRequests(world.Tenants[1]))
        {
            using var request = operation;
            using var result = await admin.SendAsync(request, ct);
            await AssertStandingRefusalAsync(result, me, ct);
        }
        using var hidden = await admin.GetAsync("/api/admin/tenants/999999", ct);
        await AssertStandingRefusalAsync(hidden, me, ct);
        using var home = await admin.PostAsync("/api/auth/exit-assume", null, ct);
        Assert.Equal(HttpStatusCode.BadRequest, home.StatusCode);
        Assert.Equal(before, await OperatorStateAsync(world, ct));
        Assert.Empty(mail.ReceivedCalls());
        Assert.Empty(storage.ReceivedCalls());
    }

    private static async Task<string> OperatorStateAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenants = await db.Set<Tenant>().IgnoreQueryFilters().OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.Name, t.ModifiedAt }).ToListAsync(ct);
        return JsonSerializer.Serialize(new { tenants, identity = await IdentityStateAsync(world, ct) });
    }

    private static IEnumerable<HttpRequestMessage> OperatorRequests(SeededTenant target)
    {
        foreach (var read in new[] { "system/version", "admin/auth/settings", "admin/email/settings", "admin/email/templates",
            "admin/platform-admins", "admin/storage", "admin/tenants", $"admin/tenants/{target.Id}" })
        {
            yield return new(HttpMethod.Get, $"/api/{read}");
        }
        yield return new(HttpMethod.Post, "/api/admin/email/test") { Content = JsonContent.Create(new { toEmail = "to@example.test", fromEmail = "from@example.test" }) };
        yield return new(HttpMethod.Post, "/api/admin/tenants") { Content = JsonContent.Create(new { name = "New tenant" }) };
        yield return new(HttpMethod.Patch, $"/api/admin/tenants/{target.Id}") { Content = JsonContent.Create(new { name = "Renamed tenant" }) };
        yield return new(HttpMethod.Post, $"/api/admin/platform-admins/{target.Creator.Id}");
        yield return new(HttpMethod.Delete, $"/api/admin/platform-admins/{target.PlatformAdmin.Id}");
        yield return new(HttpMethod.Post, "/api/auth/assume-tenant") { Content = JsonContent.Create(new { tenantId = target.Id }) };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Verified_operator_can_return_home_while_product_operations_remain_blocked(bool unavailable)
    {
        var ct = TestContext.Current.CancellationToken;
        var blocker = new ReturnHomeProvider();
        var conflict = new MutableProvider("conflict", "/other") { Pending = false };
        await using var host = SessionHost(blocker, conflict);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var admin = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var tenant = world.Tenants[1].Id;
        using var enter = await admin.PostAsJsonAsync("/api/auth/assume-tenant", new { tenantId = tenant }, ct);
        using var assumed = host.CreateClient();
        await UseSessionAsync(assumed, enter, ct);
        conflict.Pending = unavailable;
        using var denied = await assumed.GetAsync("/api/admin/auth/settings", ct);
        if (unavailable)
        {
            await AssertUnavailableExitAsync(denied, ct);
        }
        else
        {
            var me = await assumed.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
            var action = me.GetProperty("requiredActions").EnumerateArray().Single(a => a.GetProperty("key").GetString() == "profile.complete");
            Assert.False(action.GetProperty("canResolve").GetBoolean());
            Assert.Equal("blocking", action.GetProperty("severity").GetString());
            Assert.DoesNotContain("preventResolution", action.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("homeTenantId", me.ToString(), StringComparison.OrdinalIgnoreCase);
            await AssertStandingRefusalAsync(denied, me, ct);
        }
        Assert.Equal(new VerifiedAssumption(world.Tenants[0].PlatformAdmin.Id, world.Tenants[0].Id), blocker.LastAssumed?.VerifiedAssumption);
        Assert.Equal(tenant, blocker.LastAssumed?.TenantId);
        using var exit = await assumed.PostAsync("/api/auth/exit-assume", null, ct);
        conflict.Pending = false;
        using var home = host.CreateClient();
        await UseSessionAsync(home, exit, ct);
        await AssertHomeSessionAsync(home, world.Tenants[0].Id, ct);
    }

    private sealed class ReturnHomeProvider : IRequiredActionProvider
    {
        public string CacheConfigurationKey => "return-home";
        internal RequiredActionSubject? LastAssumed { get; private set; }

        public Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken)
        {
            if (subject.VerifiedAssumption is null)
            {
                return Task.FromResult<IReadOnlyList<RequiredAction>>([]);
            }
            LastAssumed = subject;
            return Task.FromResult<IReadOnlyList<RequiredAction>>([new()
            {
                Key = "profile.complete", ResolvePath = "/profile", Scope = RequiredActionScope.User,
                Severity = RequiredActionSeverity.Blocking, PreventResolution = true, BlockNonResolvers = true
            }]);
        }
    }

    [Fact]
    public async Task Exit_requires_a_distinct_persisted_home_even_after_a_successful_assumption()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = SessionHost(new ReturnHomeProvider(), new MutableProvider("optional", "/optional") { Pending = false });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var admin = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var target = world.Tenants[1].Id;
        using var enter = await admin.PostAsJsonAsync("/api/auth/assume-tenant", new { tenantId = target }, ct);
        using var assumed = host.CreateClient();
        await UseSessionAsync(assumed, enter, ct);
        using var warm = await assumed.GetAsync("/api/auth/me", ct);
        warm.EnsureSuccessStatusCode();

        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var actor = world.Tenants[0].PlatformAdmin.Id;
        await db.Users.Where(u => u.Id == actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.TenantId, target), ct);

        using var rejected = await assumed.PostAsync("/api/auth/exit-assume", null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.DoesNotContain("accessToken", await rejected.Content.ReadAsStringAsync(ct));
        using var standing = await assumed.GetAsync("/api/auth/me", ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, standing.StatusCode);
    }

    private ReportingWebHost SessionHost(IRequiredActionProvider blocker, IRequiredActionProvider conflict) =>
        new(fixture, services =>
        {
            services.AddSingleton(blocker);
            services.AddSingleton(conflict);
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("help", "/help") { Severity = RequiredActionSeverity.Notice, Key = "help" });
            EnableStanding(services);
        });

    private static async Task UseSessionAsync(HttpClient client, HttpResponseMessage issued, CancellationToken ct)
    {
        issued.EnsureSuccessStatusCode();
        var tokens = await issued.Content.ReadFromJsonAsync<JsonElement>(ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());
    }

    private static async Task AssertHomeSessionAsync(HttpClient home, long tenantId, CancellationToken ct)
    {
        var homeMe = await home.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        Assert.Equal(tenantId.ToString(), homeMe.GetProperty("tenantId").GetString());
        using var alreadyHome = await home.PostAsync("/api/auth/exit-assume", null, ct);
        Assert.Equal(HttpStatusCode.BadRequest, alreadyHome.StatusCode);
        Assert.Contains("Not in an assumed tenant session", await alreadyHome.Content.ReadAsStringAsync(ct));
    }

    private static async Task AssertUnavailableExitAsync(HttpResponseMessage denied, CancellationToken ct)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, denied.StatusCode);
        var problem = await denied.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Contains("Try again", problem.GetProperty("detail").GetString());
        Assert.False(problem.TryGetProperty("accessToken", out _));
        Assert.False(problem.TryGetProperty("refreshToken", out _));
    }

    [Fact]
    public async Task Ordinary_platform_policy_denials_precede_standing_but_self_revocation_does_not()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("one", "/one"));
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("two", "/two"));
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var admin = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var reader = await RestrictedUserAsync(world, ct);
        using var anonymous = host.CreateClient();
        using var anonymousExit = await anonymous.PostAsync("/api/auth/exit-assume", null, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousExit.StatusCode);
        using var deniedExit = await reader.PostAsync("/api/auth/exit-assume", null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, deniedExit.StatusCode);
        using var challenge = await anonymous.GetAsync("/api/admin/auth/settings", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
        using var denied = await reader.GetAsync("/api/admin/auth/settings", ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var storage = await reader.GetAsync("/api/admin/storage", ct);
        Assert.Equal(HttpStatusCode.Forbidden, storage.StatusCode);
        using var self = await admin.DeleteAsync($"/api/admin/platform-admins/{world.Tenants[0].PlatformAdmin.Id}", ct);
        await AssertUnavailableQuestionsAsync(self, ct);
        using var retry = await admin.GetAsync("/api/admin/auth/settings", ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, retry.StatusCode);
        Assert.Contains("Try again", await retry.Content.ReadAsStringAsync(ct));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Storage_dashboard_counts_only_the_current_tenants_submissions(int tenantIndex)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, tenantIndex, IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var other = await world.AsAsync(TestPersona.TenantAdmin, 1 - tenantIndex, IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var own = await SubmissionTarget.CreateAsync(owner, ct);
        await SubmissionTarget.CreateAsync(other, ct);

        // Act
        var dashboard = await owner.GetFromJsonAsync<JsonElement>("/api/admin/storage", ct);

        // Assert
        var form = Assert.Single(dashboard.GetProperty("formStats").EnumerateArray());
        Assert.Equal(own.Form, form.GetProperty("formId").ToString());
        Assert.Equal("1", dashboard.GetProperty("tenantStats").GetProperty("submissionCount").ToString());
        Assert.True(long.Parse(form.GetProperty("estimatedStorageBytes").ToString(), System.Globalization.CultureInfo.InvariantCulture) >= 0);
        Assert.Empty(dashboard.GetProperty("tableStats").EnumerateArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admitted_operator_requests_keep_the_ordinary_operation_results(bool notice)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var ordinary = new ReportingWebHost(fixture);
        var baseline = await OperatorOutcomesAsync(ordinary, ct);
        await using var admitted = new ReportingWebHost(fixture, services =>
        {
            services.AddRequiredActionEnforcement();
            if (notice)
            {
                services.AddSingleton<IRequiredActionProvider>(new MutableProvider("help", "/help") { Severity = RequiredActionSeverity.Notice });
            }
        });

        // Act
        var outcomes = await OperatorOutcomesAsync(admitted, ct);

        // Assert
        Assert.Equal(baseline, outcomes);
    }

    private static async Task<HttpStatusCode[]> OperatorOutcomesAsync(ReportingWebHost host, CancellationToken ct)
    {
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var admin = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        List<HttpStatusCode> outcomes = [];
        foreach (var operation in OperatorRequests(world.Tenants[1]))
        {
            using var request = operation;
            using var result = await admin.SendAsync(request, ct);
            outcomes.Add(result.StatusCode);
        }
        return [.. outcomes];
    }
}

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
        await AssertStandingRefusalAsync(home, me, ct);
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
    public async Task Assumed_session_checks_standing_before_issuing_home_tokens(bool unavailable)
    {
        var ct = TestContext.Current.CancellationToken;
        var blocker = new MutableProvider("legal", "/terms") { Pending = false };
        var conflict = new MutableProvider("conflict", "/other") { Pending = false };
        await using var host = SessionHost(blocker, conflict);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var admin = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var tenant = world.Tenants[1].Id;
        using var enter = await admin.PostAsJsonAsync("/api/auth/assume-tenant", new { tenantId = tenant }, ct);
        using var assumed = host.CreateClient();
        await UseSessionAsync(assumed, enter, ct);
        blocker.Pending = true;
        conflict.Pending = unavailable;
        var before = await OperatorStateAsync(world, ct);
        using var denied = await assumed.PostAsync("/api/auth/exit-assume", null, ct);
        if (unavailable)
        {
            await AssertUnavailableExitAsync(denied, ct);
        }
        else
        {
            var me = await assumed.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
            await AssertStandingRefusalAsync(denied, me, ct);
        }
        Assert.Equal(before, await OperatorStateAsync(world, ct));

        blocker.Pending = conflict.Pending = false;
        using var scope = world.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICurrentUserAuthorizationService>()
            .InvalidateAuthorizationDataCacheAsync(world.Tenants[0].PlatformAdmin.Id.ToString(), tenant, ct);
        using var exit = await assumed.PostAsync("/api/auth/exit-assume", null, ct);
        using var home = host.CreateClient();
        await UseSessionAsync(home, exit, ct);
        await AssertHomeSessionAsync(home, world.Tenants[0].Id, ct);
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

    [Fact]
    public async Task Eligible_operator_requests_consult_standing_once_before_dispatching_product_work()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AuthorizationData { RequiredActionsUnavailable = true }));
        await using var host = new ReportingWebHost(fixture, services =>
            services.AddScoped(_ => new AccountStanding(snapshots)));
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var admin = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        foreach (var operation in OperatorRequests(world.Tenants[1]))
        {
            using var request = operation;
            using var result = await admin.SendAsync(request, ct);
            Assert.True(result.StatusCode == HttpStatusCode.ServiceUnavailable, Operation(result));
            await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
            snapshots.ClearReceivedCalls();
        }
        using var missing = await admin.PatchAsJsonAsync("/api/admin/tenants/999999", new { name = "missing" }, ct);
        await AssertUnavailableQuestionsAsync(missing, ct);
        await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        var patch = $"/api/admin/tenants/{world.Tenants[1].Id}";
        foreach (var field in new object[] { new { allowSelfRegistration = false },
                     new { allowedAuthProviderKeys = Array.Empty<string>() }, new { defaultRegistrationRoleName = "Respondent" } })
        {
            using var settings = await admin.PatchAsJsonAsync(patch, field, ct);
            await AssertUnavailableQuestionsAsync(settings, ct);
        }
        await snapshots.Received(4).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
    }
}

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
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
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

    [Fact]
    public async Task Assumed_session_checks_standing_before_issuing_home_tokens()
    {
        var ct = TestContext.Current.CancellationToken;
        var blocker = new MutableProvider("legal", "/terms") { Pending = false };
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(blocker);
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("help", "/help") { Severity = RequiredActionSeverity.Notice, Key = "help" });
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var admin = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var tenant = world.Tenants[1].Id;
        using var enter = await admin.PostAsJsonAsync("/api/auth/assume-tenant", new { tenantId = tenant }, ct);
        enter.EnsureSuccessStatusCode();
        var tokens = await enter.Content.ReadFromJsonAsync<JsonElement>(ct);
        using var assumed = host.CreateClient();
        assumed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());
        blocker.Pending = true;
        var me = await assumed.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        var before = await OperatorStateAsync(world, ct);
        using var denied = await assumed.PostAsync("/api/auth/exit-assume", null, ct);
        await AssertStandingRefusalAsync(denied, me, ct);
        Assert.Equal(before, await OperatorStateAsync(world, ct));

        blocker.Pending = false;
        using var scope = world.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICurrentUserAuthorizationService>()
            .InvalidateAuthorizationDataCacheAsync(world.Tenants[0].PlatformAdmin.Id.ToString(), tenant, ct);
        using var exit = await assumed.PostAsync("/api/auth/exit-assume", null, ct);
        exit.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Ordinary_platform_and_tenant_denials_precede_unavailable_standing()
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
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        Assert.Contains("cannot revoke your own", await self.Content.ReadAsStringAsync(ct));
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
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Empty(snapshots.ReceivedCalls());
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Infrastructure.Identity.Authorization.Strategies;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Forwarded_strategy_supports_members_but_cannot_attest_to_an_assumption()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new ReturnHomeProvider();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.RemoveAll<IAuthorizationStrategy>();
            services.AddScoped<DefaultAuthorization>();
            services.AddScoped<IAuthorizationStrategy, ForwardedStrategy>();
            services.AddSingleton<IRequiredActionProvider>(provider);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var member = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var me = await member.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        Assert.Empty(me.GetProperty("requiredActions").EnumerateArray());
        using var ordinaryExit = await member.PostAsync("/api/auth/exit-assume", null, ct);
        Assert.Equal(HttpStatusCode.BadRequest, ordinaryExit.StatusCode);
        using var enter = await member.PostAsJsonAsync("/api/auth/assume-tenant", new { tenantId = world.Tenants[1].Id }, ct);
        using var assumed = host.CreateClient();
        await UseSessionAsync(assumed, enter, ct);

        using var standing = await assumed.GetAsync("/api/auth/me", ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, standing.StatusCode);
        var before = await StoredOperatorSessionAsync(world, ct);
        Assert.False(string.IsNullOrEmpty(before));
        using var exit = await assumed.PostAsync("/api/auth/exit-assume", null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, exit.StatusCode);
        Assert.DoesNotContain("accessToken", await exit.Content.ReadAsStringAsync(ct));
        Assert.True(before == await StoredOperatorSessionAsync(world, ct), "A refused exit must not rotate the stored refresh token.");
        Assert.Null(provider.LastAssumed);
    }

    private static async Task<string?> StoredOperatorSessionAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        return await db.Users.Where(u => u.Id == world.Tenants[0].PlatformAdmin.Id).Select(u => u.RefreshTokenHash).SingleAsync(ct);
    }

    private sealed class ForwardedStrategy(DefaultAuthorization inner) : IAuthorizationStrategy
    {
        public bool CanHandle(ClaimsPrincipal principal) => inner.CanHandle(principal);
        public Task<Result<AuthorizationData>> GetAuthorizationDataAsync(ClaimsPrincipal principal, CancellationToken cancellationToken) =>
            inner.GetAuthorizationDataAsync(principal, cancellationToken);
    }
}

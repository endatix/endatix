using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Unlock_retries_without_mutation_when_standing_is_unavailable_then_succeeds_with_a_notice()
    {
        var ct = TestContext.Current.CancellationToken;
        var notice = new MutableProvider("notice", "/help") { Severity = RequiredActionSeverity.Notice };
        var conflict = new MutableProvider("conflict", "/other") { Pending = false };
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(notice);
            services.AddSingleton<IRequiredActionProvider>(conflict);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var route = $"/api/users/{world.Tenants[0].Creator.Id}";
        using var locked = await admin.PostAsync($"{route}/lockout", null, ct);
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        conflict.Pending = true;
        await InvalidateAsync(world, ct);

        using var unknown = await admin.PostAsync($"{route}/unlock", null, ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unknown.StatusCode);
        var problem = await unknown.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Contains("Try again", problem.GetProperty("detail").GetString());
        Assert.False(problem.TryGetProperty("requiredActions", out _));
        conflict.Pending = false;
        await InvalidateAsync(world, ct);

        var before = await admin.GetFromJsonAsync<JsonElement>(route, ct);
        Assert.True(before.GetProperty("isLockedOut").GetBoolean());
        using var allowed = await admin.PostAsync($"{route}/unlock", null, ct);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var after = await admin.GetFromJsonAsync<JsonElement>(route, ct);
        Assert.False(after.GetProperty("isLockedOut").GetBoolean());
    }

    [Fact]
    public async Task Lockout_preserves_self_denial_and_hidden_users_before_checking_standing()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new MutableProvider("profile", "/profile");
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(provider);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var own = world.Tenants[0];
        using var self = await admin.PostAsync($"/api/users/{own.Admin.Id}/lockout", null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        var denied = await self.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Contains("your own account", denied.GetProperty("detail").GetString());
        using var hidden = await admin.PostAsync($"/api/users/{world.Tenants[1].Creator.Id}/lockout", null, ct);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var me = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        var route = $"/api/users/{own.Creator.Id}";
        using var refused = await admin.PostAsync($"{route}/lockout", null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("required_action", problem.GetProperty("errorCode").GetString());
        Assert.True(JsonElement.DeepEquals(me.GetProperty("requiredActions"), problem.GetProperty("requiredActions")));

        provider.Pending = false;
        await InvalidateAsync(world, ct);
        var before = await admin.GetFromJsonAsync<JsonElement>(route, ct);
        Assert.False(before.GetProperty("isLockedOut").GetBoolean());
        using var allowed = await admin.PostAsync($"{route}/lockout", null, ct);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var after = await admin.GetFromJsonAsync<JsonElement>(route, ct);
        Assert.True(after.GetProperty("isLockedOut").GetBoolean());
    }
}

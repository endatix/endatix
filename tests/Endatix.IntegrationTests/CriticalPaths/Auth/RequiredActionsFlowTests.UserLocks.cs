using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Admitted_user_manager_gets_the_ordinary_role_denial()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(new MutableProvider("profile", "/profile") { Severity = RequiredActionSeverity.Notice });
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var manager = await world.UserManagerClientAsync(ct);
        var id = world.Tenants[0].Admin.Id;
        var before = await LockStateAsync(world, id, ct);

        // Act
        using var denied = await manager.PostAsync($"/api/users/{id}/lockout", null, ct);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var problem = await denied.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Contains("You can only grant access you hold yourself.", problem.GetProperty("detail").GetString());
        Assert.False(problem.TryGetProperty("requiredActions", out _));
        Assert.Equal(before, await LockStateAsync(world, id, ct));
    }

    [Fact]
    public async Task An_authorized_admin_can_lock_a_user_when_no_action_provider_is_registered()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, EnableStanding);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var id = world.Tenants[0].Creator.Id;
        using var allowed = await admin.PostAsync($"/api/users/{id}/lockout", null, ct);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var (enabled, end) = await LockStateAsync(world, id, ct);
        Assert.True(enabled && end > DateTimeOffset.UtcNow);
    }

    private static async Task<(bool Enabled, DateTimeOffset? End)> LockStateAsync(IntegrationTestWorld world, long id, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(item => item.Id == id, ct);
        return (user.LockoutEnabled, user.LockoutEnd);
    }

    private static async Task DisableLockoutAsync(IntegrationTestWorld world, long id, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var user = await db.Users.SingleAsync(item => item.Id == id, ct);
        user.LockoutEnabled = false;
        await db.SaveChangesAsync(ct);
    }

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
        var lockedState = await LockStateAsync(world, world.Tenants[0].Creator.Id, ct);
        conflict.Pending = true;
        await InvalidateAsync(world, ct);

        using var unknown = await admin.PostAsync($"{route}/unlock", null, ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unknown.StatusCode);
        var problem = await unknown.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Contains("Try again", problem.GetProperty("detail").GetString());
        Assert.False(problem.TryGetProperty("requiredActions", out _));
        Assert.Equal(lockedState, await LockStateAsync(world, world.Tenants[0].Creator.Id, ct));
        conflict.Pending = false;
        await InvalidateAsync(world, ct);

        var before = await admin.GetFromJsonAsync<JsonElement>(route, ct);
        Assert.True(before.GetProperty("isLockedOut").GetBoolean());
        using var allowed = await admin.PostAsync($"{route}/unlock", null, ct);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var operation = await allowed.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.True(operation.GetProperty("isSuccess").GetBoolean());
        Assert.Equal("User unlocked.", operation.GetProperty("message").GetString());
        var after = await admin.GetFromJsonAsync<JsonElement>(route, ct);
        Assert.False(after.GetProperty("isLockedOut").GetBoolean());
    }

    [Fact]
    public async Task An_administrator_cannot_lock_their_own_account()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var id = world.Tenants[0].Admin.Id;
        var before = await LockStateAsync(world, id, ct);

        // Act
        using var self = await admin.PostAsync($"/api/users/{id}/lockout", null, ct);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        var problem = await self.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Contains("your own account", problem.GetProperty("detail").GetString());
        Assert.Equal(before, await LockStateAsync(world, id, ct));
    }

    [Theory]
    [InlineData("lockout")]
    [InlineData("unlock")]
    public async Task Tenant_lock_changes_preserve_visibility_on_the_ordinary_host(string operation)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var id = world.Tenants[1].Creator.Id;
        var before = await LockStateAsync(world, id, ct);

        // Act
        using var response = await admin.PostAsync($"/api/users/{id}/{operation}", null, ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await LockStateAsync(world, id, ct));
    }

    [Fact]
    public async Task Lockout_resumes_in_the_same_session_after_clearance()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new MutableProvider("profile", "/profile") { Pending = false };
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(provider);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var own = world.Tenants[0];
        provider.Pending = true;
        await InvalidateAsync(world, ct);

        var me = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        var route = $"/api/users/{own.Creator.Id}";
        await DisableLockoutAsync(world, own.Creator.Id, ct);
        using var refused = await admin.PostAsync($"{route}/lockout", null, ct);
        await AssertStandingRefusalAsync(refused, me, ct);
        Assert.Equal((false, (DateTimeOffset?)null), await LockStateAsync(world, own.Creator.Id, ct));

        provider.Pending = false;
        await InvalidateAsync(world, ct);
        var before = await admin.GetFromJsonAsync<JsonElement>(route, ct);
        Assert.False(before.GetProperty("isLockedOut").GetBoolean());
        using var allowed = await admin.PostAsync($"{route}/lockout", null, ct);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var operation = await allowed.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.True(operation.GetProperty("isSuccess").GetBoolean());
        Assert.Equal("User locked out.", operation.GetProperty("message").GetString());
        var after = await admin.GetFromJsonAsync<JsonElement>(route, ct);
        Assert.True(after.GetProperty("isLockedOut").GetBoolean());
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Tenant_and_system_changes_refresh_the_next_request_on_another_warm_host()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var action = new MutableProvider("freshness", "/profile");
        await using var reader = new ReportingWebHost(fixture, services => services.AddSingleton<IRequiredActionProvider>(action));
        var world = await reader.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var first = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var second = await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 1, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        Assert.Equal(1, await PendingCountAsync(first, ct));
        Assert.Equal(1, await PendingCountAsync(second, ct));
        using var writer = fixture.Factory.Services.CreateScope();
        var changes = writer.ServiceProvider.GetRequiredService<IAuthorizationDataChanges>();

        // Act / Assert
        action.Pending = false;
        await changes.TenantChangedAsync(world.Tenants[0].Id, ct);
        Assert.Equal(0, await PendingCountAsync(first, ct));
        Assert.Equal(1, await PendingCountAsync(second, ct));
        await changes.AllChangedAsync(ct);
        Assert.Equal(0, await PendingCountAsync(second, ct));
    }

    private static async Task<int> PendingCountAsync(HttpClient client, CancellationToken ct) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/auth/me", ct)).GetProperty("requiredActions").GetArrayLength();
}

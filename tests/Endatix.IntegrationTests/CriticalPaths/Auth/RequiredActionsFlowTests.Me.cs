using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Authorization;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Me_reports_no_enforcement_on_a_host_without_a_standing_registration()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services => services.RemoveAll<AccountStanding>());
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);

        // Act
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);

        // Assert
        Assert.False(me.GetProperty("requiredActionsEnforced").GetBoolean());
        Assert.Equal(0, me.GetProperty("requiredActions").GetArrayLength());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Me_reports_the_serving_hosts_enforcement_even_with_a_shared_warm_snapshot(bool pending)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var provider = new MutableProvider("v1", "/profile") { Pending = pending };
        await using var dormant = new ReportingWebHost(fixture, services => services.AddSingleton<IRequiredActionProvider>(provider));
        var world = await dormant.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        await using var active = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(provider);
            services.AddSingleton(dormant.Services.GetRequiredService<HybridCache>());
            services.AddRequiredActionEnforcement();
        });
        using var first = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var second = active.CreateClient();
        second.DefaultRequestHeaders.Authorization = first.DefaultRequestHeaders.Authorization;

        // Act
        var off = await first.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        var on = await second.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        var again = await first.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);

        // Assert
        Assert.False(off.GetProperty("requiredActionsEnforced").GetBoolean());
        Assert.True(on.GetProperty("requiredActionsEnforced").GetBoolean());
        Assert.Equal(pending ? 1 : 0, off.GetProperty("requiredActions").GetArrayLength());
        Assert.True(JsonNode.DeepEquals(SharedSnapshot(off), SharedSnapshot(on)));
        Assert.NotEqual(off.GetProperty("eTag").GetString(), on.GetProperty("eTag").GetString());
        Assert.True(JsonElement.DeepEquals(off, again));
        TestContext.Current.TestOutputHelper?.WriteLine($"Dormant: {off.GetRawText()}\nEnforced: {on.GetRawText()}");
    }

    private static JsonObject SharedSnapshot(JsonElement response)
    {
        var snapshot = response.Deserialize<JsonObject>()!;
        snapshot.Remove("requiredActionsEnforced");
        snapshot.Remove("eTag");
        return snapshot;
    }
}

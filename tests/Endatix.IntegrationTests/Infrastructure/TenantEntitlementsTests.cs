using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Multitenancy;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class TenantEntitlementsTests(EndatixIntegrationWebHostFixture fixture)
{
    [Fact]
    public async Task Host_allows_every_entitlement_by_default()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<ITenantEntitlements>();

        var allowed = await entitlements.IsEntitledAsync(1, EntitlementNames.BRANDING, TestContext.Current.CancellationToken);

        entitlements.Should().BeOfType<AllowAllTenantEntitlements>();
        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Host_registered_implementation_replaces_the_default()
    {
        await using var factory = new EndatixWebApplicationFactory(
                fixture.Database.ConnectionString,
                fixture.Database.Provider)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddScoped<ITenantEntitlements, NoBrandingTenantEntitlements>()));
        await using var scope = factory.Services.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<ITenantEntitlements>();

        var branding = await entitlements.IsEntitledAsync(1, EntitlementNames.BRANDING, TestContext.Current.CancellationToken);

        entitlements.Should().BeOfType<NoBrandingTenantEntitlements>();
        branding.Should().BeFalse();
    }

    private sealed class NoBrandingTenantEntitlements : ITenantEntitlements
    {
        public Task<bool> IsEntitledAsync(long tenantId, string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(name != EntitlementNames.BRANDING);
    }
}

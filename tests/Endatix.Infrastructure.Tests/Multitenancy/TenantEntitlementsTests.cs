using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Multitenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Multitenancy;

public sealed class TenantEntitlementsTests
{
    [Theory]
    [InlineData(Entitlements.BRANDING)]
    [InlineData("a-capability-not-named-yet")]
    public async Task Default_allows_every_entitlement(string name)
    {
        await using var provider = new ServiceCollection().AddTenantEntitlements().BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<ITenantEntitlements>();

        var allowed = await entitlements.HasAsync(42, name, TestContext.Current.CancellationToken);

        entitlements.Should().BeOfType<AllowAllTenantEntitlements>();
        allowed.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Default_rejects_a_missing_name(string? name)
    {
        var entitlements = new AllowAllTenantEntitlements();

        var ask = () => entitlements.HasAsync(42, name!, TestContext.Current.CancellationToken);

        await ask.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Host_registered_before_the_default_keeps_its_implementation()
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantEntitlements, DenyAllTenantEntitlements>();

        services.AddTenantEntitlements();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<ITenantEntitlements>();
        entitlements.Should().BeOfType<DenyAllTenantEntitlements>();
        (await entitlements.HasAsync(42, Entitlements.BRANDING, TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public void Default_is_registered_scoped()
    {
        var services = new ServiceCollection().AddTenantEntitlements();

        services.Should().ContainSingle(d => d.ServiceType == typeof(ITenantEntitlements))
            .Which.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    private sealed class DenyAllTenantEntitlements : ITenantEntitlements
    {
        public Task<bool> HasAsync(long tenantId, string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}

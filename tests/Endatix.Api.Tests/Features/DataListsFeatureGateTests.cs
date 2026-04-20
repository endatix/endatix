using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Framework.Configuration;
using Endatix.Framework.FeatureFlags;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Api.Tests.Features;

public sealed class DataListsFeatureGateTests
{
    private static ServiceCollection CreateServices(Dictionary<string, string?> configValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEndatixOptions<FeatureFlagsOptions>(configuration);
        services.AddEndatixFeatureFlags(configuration);
        services.AddScoped<ITenantContext>(_ => new TestTenantContext());
        services.AddScoped<IUserContext>(_ => new TestUserContext());

        return services;
    }

    [Fact]
    public async Task IsEnabledAsync_returns_false_when_disabled()
    {
        var services = CreateServices(new Dictionary<string, string?>
        {
            ["Endatix:FeatureFlags:DataLists"] = "false"
        });

        await using var provider = services.BuildServiceProvider();
        var gate = provider.GetRequiredService<IFeatureGate>();

        var enabled = await gate.IsEnabledAsync(FeatureFlags.DataLists, TestContext.Current.CancellationToken);
        Assert.False(enabled);
    }

    [Fact]
    public async Task IsEnabledAsync_returns_true_when_enabled()
    {
        var services = CreateServices(new Dictionary<string, string?>
        {
            ["Endatix:FeatureFlags:DataLists"] = "true"
        });

        await using var provider = services.BuildServiceProvider();
        var gate = provider.GetRequiredService<IFeatureGate>();

        var enabled = await gate.IsEnabledAsync(FeatureFlags.DataLists, TestContext.Current.CancellationToken);
        Assert.True(enabled);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public long TenantId => 1;
    }

    private sealed class TestUserContext : IUserContext
    {
        public bool IsAnonymous => false;
        public bool IsAuthenticated => true;
        public string? GetCurrentUserId() => "test-user";
        public User? GetCurrentUser() => new User(1, "test-user", "test@example.com", true);
    }
}

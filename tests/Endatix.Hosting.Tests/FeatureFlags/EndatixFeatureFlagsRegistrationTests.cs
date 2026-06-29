using Endatix.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Endatix.Hosting.Tests.FeatureFlags;

public sealed class EndatixFeatureFlagsRegistrationTests
{
    [Fact]
    public void AddEndatix_RegistersFeatureFlagTargetingContextAccessor()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddEndatix(configuration);

        // Assert
        services.Should().Contain(service => service.ServiceType == typeof(ITargetingContextAccessor));
    }
}

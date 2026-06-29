using Endatix.Framework.FeatureFlags;
using Endatix.Infrastructure.FeatureFlags;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Endatix.Infrastructure.Tests.FeatureFlags;

public sealed class FeatureFlagsRegistrationTests
{
    [Fact]
    public void AddEndatixFeatureFlags_WithEndatixTargeting_RegistersTargetingContextAccessor()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddEndatixFeatureFlags(configuration)
            .WithEndatixTargeting();

        // Assert
        var descriptor = services.FirstOrDefault(service =>
            service.ServiceType == typeof(ITargetingContextAccessor)
            && service.ImplementationType == typeof(FeatureFlagsTargetingContext));

        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddEndatixFeatureFlags_WithoutWithEndatixTargeting_DoesNotRegisterTargetingContextAccessor()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddEndatixFeatureFlags(configuration);

        // Assert
        services.Should().NotContain(service => service.ServiceType == typeof(ITargetingContextAccessor));
    }
}

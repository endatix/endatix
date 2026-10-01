using Endatix.Modules.Jobs.Endpoints;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Dashboard.Services;

namespace Endatix.Modules.Jobs.Tests.Endpoints;

public sealed class QuartzDashboardTests
{
    private static readonly string JobClass = typeof(BackgroundJobExecution).FullName!;

    [Fact]
    public void IsEndatixJobClass_ExactOrAssemblyQualifiedName_IsAllowed()
    {
        // Arrange
        var names = new[] { JobClass, $"{JobClass}, {typeof(BackgroundJobExecution).Assembly.GetName().Name}" };

        // Act
        var allowed = names.Select(QuartzDashboard.IsEndatixJobClass);

        // Assert
        allowed.Should().AllSatisfy(result => result.Should().BeTrue());
    }

    [Theory]
    [InlineData("X")]
    [InlineData("+Nested")]
    [InlineData("X, Other.Assembly")]
    public void IsEndatixJobClass_NameThatOnlyStartsWithTheJobClass_IsRefused(string suffix)
    {
        // Arrange
        var name = JobClass + suffix;

        // Act
        var allowed = QuartzDashboard.IsEndatixJobClass(name);

        // Assert
        allowed.Should().BeFalse();
    }

    [Fact]
    public void AddQuartzDashboard_OnItsOwn_RegistersAttachedStoreDiscovery()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddQuartzDashboard();

        // Assert — fails if Quartz renames the service or stops registering it, so the removal is never silently moot.
        services.Should().ContainSingle(descriptor => QuartzDashboard.IsAttachedStoreDiscovery(descriptor));
    }

    [Fact]
    public void AddJobsDashboard_Enabled_RegistersTheDashboardWithoutAttachedStoreDiscovery()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endatix:BackgroundJobs:Dashboard:Enabled"] = "true",
            })
            .Build();

        // Act
        services.AddJobsDashboard(configuration);

        // Assert
        services.Should().NotContain(descriptor => QuartzDashboard.IsAttachedStoreDiscovery(descriptor));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IQuartzApiClient));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IStartupFilter));
    }
}

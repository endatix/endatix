using System.Collections;
using System.Reflection;
using Endatix.Api.Builders;
using Endatix.Api.Infrastructure;
using Endatix.Framework.Modules;
using Endatix.Hosting.Builders;
using FastEndpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Hosting.Tests.Builders;

public class EndatixBuilderUseModuleTests
{
    [Fact]
    public void UseModule_SameAssemblyTwice_AddsModuleOnlyOnce()
    {
        // Arrange
        var builder = CreateBuilder();
        var firstModule = new TrackingTestModule();
        var secondModule = new TrackingTestModule();

        // Act
        builder.UseModule(firstModule);
        builder.UseModule(secondModule);

        // Assert
        GetRegisteredModuleCount(builder).Should().Be(1);
    }

    [Fact]
    public void UseModule_SameInstanceTwice_AddsModuleOnlyOnce()
    {
        // Arrange
        var builder = CreateBuilder();
        var module = new TrackingTestModule();

        // Act
        builder.UseModule(module);
        builder.UseModule(module);

        // Assert
        GetRegisteredModuleCount(builder).Should().Be(1);
    }

    [Fact]
    public void UseModule_FirstRegistration_AddsModule()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        builder.UseModule(new TrackingTestModule());

        // Assert
        GetRegisteredModuleCount(builder).Should().Be(1);
    }

    [Fact]
    public void UseModule_ModuleWithFastEndpointsConfigAndFlagEnabled_AppliesEndpointConfiguration()
    {
        // Arrange
        var builder = CreateBuilder(featureFlagEnabled: true);
        var module = new EndpointConfiguringTestModule();

        // Act
        builder.UseModule(module);
        InvokeConfiguredFastEndpoints(builder);

        // Assert
        module.ConfigureFastEndpointsCallCount.Should().Be(1);
    }

    [Fact]
    public void UseModule_ModuleWithFastEndpointsConfigAndFlagDisabled_DoesNotApplyEndpointConfiguration()
    {
        // Arrange
        var builder = CreateBuilder(featureFlagEnabled: false);
        var module = new EndpointConfiguringTestModule();

        // Act
        builder.UseModule(module);
        InvokeConfiguredFastEndpoints(builder);

        // Assert
        module.ConfigureFastEndpointsCallCount.Should().Be(0);
    }

    [Fact]
    public void UseModule_ModuleWithoutFastEndpointsConfig_RegistersNoEndpointConfiguration()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        builder.UseModule(new TrackingTestModule());

        // Assert
        GetConfigureFastEndpoints(builder).Should().BeNull();
    }

    private static EndatixBuilder CreateBuilder()
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration[Arg.Any<string>()].Returns((string?)null);
        configuration.GetSection(Arg.Any<string>()).Returns(Substitute.For<IConfigurationSection>());
        return new EndatixBuilder(new ServiceCollection(), configuration);
    }

    private static EndatixBuilder CreateBuilder(bool featureFlagEnabled)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"Endatix:FeatureFlags:{EndpointConfiguringTestModule.TestFeatureFlag}"] =
                    featureFlagEnabled.ToString()
            })
            .Build();

        return new EndatixBuilder(new ServiceCollection(), configuration);
    }

    private static int GetRegisteredModuleCount(EndatixBuilder builder)
    {
        FieldInfo? modulesField = typeof(EndatixBuilder).GetField("_modules", BindingFlags.Instance | BindingFlags.NonPublic);
        IList modules = (IList)modulesField!.GetValue(builder)!;
        return modules.Count;
    }

    /// <summary>
    /// Reads the FastEndpoints configurator the API builder has composed so far, or null when no
    /// module contributed one.
    /// </summary>
    private static Action<Config>? GetConfigureFastEndpoints(EndatixBuilder builder)
    {
        FieldInfo? optionsField = typeof(EndatixApiBuilder).GetField("_apiOptions", BindingFlags.Instance | BindingFlags.NonPublic);
        var apiOptions = (ApiOptions)optionsField!.GetValue(builder.Api)!;
        return apiOptions.ConfigureFastEndpoints;
    }

    private static void InvokeConfiguredFastEndpoints(EndatixBuilder builder) =>
        GetConfigureFastEndpoints(builder)?.Invoke(new Config());

    private sealed class TrackingTestModule : IEndatixModule
    {
        public Assembly Assembly => typeof(TrackingTestModule).Assembly;

        public void ConfigureServices(EndatixModuleBuilder builder)
        {
        }
    }

    private sealed class EndpointConfiguringTestModule : IEndatixModule, IHasFeatureFlag, IHasFastEndpointsConfig
    {
        public const string TestFeatureFlag = "UseModuleEndpointConfigTestModule";

        public Assembly Assembly => typeof(EndpointConfiguringTestModule).Assembly;

        public string FeatureFlag => TestFeatureFlag;

        public int ConfigureFastEndpointsCallCount { get; private set; }

        public void ConfigureFastEndpoints(Config config) => ConfigureFastEndpointsCallCount++;

        public void ConfigureServices(EndatixModuleBuilder builder)
        {
        }
    }
}

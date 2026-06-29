using System.Collections;
using System.Reflection;
using Endatix.Framework.Modules;
using Endatix.Hosting.Builders;
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

    private static EndatixBuilder CreateBuilder()
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration[Arg.Any<string>()].Returns((string?)null);
        configuration.GetSection(Arg.Any<string>()).Returns(Substitute.For<IConfigurationSection>());
        return new EndatixBuilder(new ServiceCollection(), configuration);
    }

    private static int GetRegisteredModuleCount(EndatixBuilder builder)
    {
        FieldInfo? modulesField = typeof(EndatixBuilder).GetField("_modules", BindingFlags.Instance | BindingFlags.NonPublic);
        IList modules = (IList)modulesField!.GetValue(builder)!;
        return modules.Count;
    }

    private sealed class TrackingTestModule : IEndatixModule
    {
        public Assembly Assembly => typeof(TrackingTestModule).Assembly;

        public void ConfigureServices(EndatixModuleBuilder builder)
        {
        }
    }
}

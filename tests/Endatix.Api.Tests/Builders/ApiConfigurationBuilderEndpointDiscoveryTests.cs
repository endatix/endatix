using System.Reflection;
using Endatix.Api.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Api.Tests.Builders;

public sealed class ApiConfigurationBuilderEndpointDiscoveryTests
{
    private static readonly Assembly ApiAssembly = typeof(ApiConfigurationBuilder).Assembly;
    private static readonly Assembly ModuleAssembly = typeof(ApiConfigurationBuilderEndpointDiscoveryTests).Assembly;

    [Fact]
    public void UseDefaults_RegistersFastEndpointsExactlyOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new ApiConfigurationBuilder(services).UseDefaults();

        // Assert
        CountEndpointDataRegistrations(services).Should().Be(1);
    }

    [Fact]
    public void ScanAssemblies_AfterUseDefaults_ReplacesTheRegistrationInsteadOfAddingOne()
    {
        // Arrange
        // AddFastEndpoints registers EndpointData, CommandHandlerRegistry, EventBus<> and Cfg with
        // AddSingleton rather than TryAdd, so a repeated call would leave a second set behind and
        // re-run an eager reflection pass over every accumulated assembly.
        var services = new ServiceCollection();
        var builder = new ApiConfigurationBuilder(services);
        builder.UseDefaults();

        // Act
        builder.ScanAssemblies(ModuleAssembly);

        // Assert
        CountEndpointDataRegistrations(services).Should().Be(1);
        GetDiscoveryAssemblies(services).Should().Contain([ApiAssembly, ModuleAssembly]);
    }

    [Fact]
    public void ScanAssemblies_SameAssemblyTwice_DoesNotReRegister()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ApiConfigurationBuilder(services);
        builder.UseDefaults();
        builder.ScanAssemblies(ModuleAssembly);
        var descriptorCount = services.Count;

        // Act
        builder.ScanAssemblies(ModuleAssembly);

        // Assert
        services.Count.Should().Be(descriptorCount);
        CountEndpointDataRegistrations(services).Should().Be(1);
    }

    [Fact]
    public void ScanAssemblies_FromASecondBuilder_KeepsTheFirstBuildersAssemblies()
    {
        // Arrange
        // AddApiEndpoints and GetApiBuilder each hand out a fresh builder over the same services.
        // With the assembly set held per builder, the second registration replaced the first and
        // its endpoints silently disappeared.
        var services = new ServiceCollection();
        new ApiConfigurationBuilder(services).UseDefaults();

        // Act
        new ApiConfigurationBuilder(services).ScanAssemblies(ModuleAssembly);

        // Assert
        GetDiscoveryAssemblies(services).Should().Contain([ApiAssembly, ModuleAssembly]);
        CountEndpointDataRegistrations(services).Should().Be(1);
    }

    [Fact]
    public void ScanAssemblies_BeforeUseDefaults_IsIncludedInTheRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ApiConfigurationBuilder(services);

        // Act
        builder.ScanAssemblies(ModuleAssembly);
        builder.UseDefaults();

        // Assert
        GetDiscoveryAssemblies(services).Should().Contain([ApiAssembly, ModuleAssembly]);
        CountEndpointDataRegistrations(services).Should().Be(1);
    }

    [Fact]
    public void ScanAssemblies_WithoutRegistering_DoesNotRegisterFastEndpoints()
    {
        // Arrange
        // Accumulating must stay side-effect free until something asks for the registration,
        // otherwise the single-registration guarantee depends on call order.
        var services = new ServiceCollection();

        // Act
        new ApiConfigurationBuilder(services).ScanAssemblies(ModuleAssembly);

        // Assert
        CountEndpointDataRegistrations(services).Should().Be(0);
    }

    /// <summary>
    /// Counts the live FastEndpoints core registrations. EndpointData is internal to
    /// FastEndpoints, so it is matched by name; the callers assert a non-zero count wherever a
    /// registration is expected, so a rename fails the tests rather than passing vacuously.
    /// </summary>
    private static int CountEndpointDataRegistrations(IServiceCollection services) =>
        services.Count(descriptor => descriptor.ServiceType.Name == "EndpointData");

    private static IReadOnlyList<Assembly> GetDiscoveryAssemblies(IServiceCollection services) =>
        EndpointDiscoveryRegistration.GetOrAdd(services).Assemblies;
}

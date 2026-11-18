using Endatix.Core.Branding;
using Endatix.Infrastructure.Branding;
using Endatix.Infrastructure.Email;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Setup;

public sealed class DeploymentBrandRegistrationTests : IDisposable
{
    private readonly string _packDirectory = Directory.CreateTempSubdirectory("brand-pack-").FullName;

    public void Dispose() => Directory.Delete(_packDirectory, recursive: true);

    [Fact]
    public void AddEmailSender_WithoutAddDeploymentBrand_ResolvesTheBuiltInBrand()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        // Act
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();

        // Assert
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<DeploymentBrand>().Should().BeSameAs(DeploymentBrand.BuiltIn);
    }

    [Fact]
    public void AddDeploymentBrand_BeforeAddEmailSender_KeepsThePackReadAtRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDeploymentBrand(PackConfiguration("""{ "version": 1, "name": "Example Forms" }"""));
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();

        // Assert
        ResolvedBrandName(services).Should().Be("Example Forms");
    }

    [Fact]
    public void AddDeploymentBrand_AfterAddEmailSender_ReplacesTheLazyFallback()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();

        // Act
        services.AddDeploymentBrand(PackConfiguration("""{ "version": 1, "name": "Example Forms" }"""));

        // Assert
        services.Count(descriptor => descriptor.ServiceType == typeof(DeploymentBrand)).Should().Be(1);
        ResolvedBrandName(services).Should().Be("Example Forms");
    }

    [Fact]
    public void AddDeploymentBrand_AfterAddEmailSenderWithInvalidPack_ThrowsAtRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();

        // Act
        var register = () => services.AddDeploymentBrand(PackConfiguration("""{ "version": 1 }"""));

        // Assert
        register.Should().Throw<BrandPackException>().Which.Message.Should().Contain("/name");
    }

    private IConfiguration PackConfiguration(string pack)
    {
        File.WriteAllText(Path.Combine(_packDirectory, "brand.json"), pack);
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endatix:FeatureFlags:WhiteLabeling"] = "true",
                [BrandPackLoader.BrandDirectoryKey] = _packDirectory,
            })
            .Build();
    }

    private static string ResolvedBrandName(IServiceCollection services)
    {
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<DeploymentBrand>().ProductName;
    }
}

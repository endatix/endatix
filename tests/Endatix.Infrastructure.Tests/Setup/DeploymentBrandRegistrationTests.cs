using Endatix.Core.Branding;
using Endatix.Core.Features.Email;
using Endatix.Infrastructure.Branding;
using Endatix.Infrastructure.Email;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        ResolvedEmailIdentity(services).Should().Be(("Example Forms", "Example Forms"));
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
        ResolvedEmailIdentity(services).Should().Be(("Example Forms", "Example Forms"));
    }

    [Fact]
    public void AddEmailSenderAndTemplateSettings_Together_RegisterTheSenderOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();
        services.AddEmailTemplateSettings();

        // Assert
        services.Count(descriptor => descriptor.ServiceType == typeof(DeploymentSender)).Should().Be(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(EmailVariables)).Should().Be(1);
    }

    [Fact]
    public void AddEmailSender_ConfiguredSender_ResolvesItsAddressAndName()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endatix:Integrations:Email:Sender:Address"] = "noreply@example.com",
                ["Endatix:Integrations:Email:Sender:Name"] = "Example Team",
            })
            .Build());

        // Act
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();

        // Assert
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<DeploymentSender>().Should().Be(new DeploymentSender("noreply@example.com", "Example Team", "noreply@example.com"));
    }

    [Theory]
    [InlineData("Example <noreply@example.com>", null)]
    [InlineData("noreply@example.com", "Example\r\nBcc: x@example.com")]
    public void AddEmailSender_InvalidSender_FailsValidation(string address, string? name)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endatix:Integrations:Email:Sender:Address"] = address,
                ["Endatix:Integrations:Email:Sender:Name"] = name,
            })
            .Build());
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();
        using var provider = services.BuildServiceProvider();

        // Act
        var resolve = () => provider.GetRequiredService<DeploymentSender>();

        // Assert
        resolve.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain("Endatix:Integrations:Email:Sender");
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

    private static (string ProductName, string SenderName) ResolvedEmailIdentity(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<EmailVariables>().ProductName, provider.GetRequiredService<DeploymentSender>().Name);
    }

    private static string ResolvedBrandName(IServiceCollection services)
    {
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<DeploymentBrand>().ProductName;
    }
}

using Endatix.Core.Branding;
using Endatix.Infrastructure.Email;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Setup;

public sealed class DeploymentBrandRegistrationTests
{
    [Fact]
    public void AddEmailSender_WithoutAddDeploymentBrand_StillResolvesABrand()
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
    public void AddDeploymentBrand_ThenAddEmailSender_KeepsTheBrandReadAtStartup()
    {
        // Arrange
        var services = new ServiceCollection();
        var startupBrand = DeploymentBrand.BuiltIn with { ProductName = "Example Forms" };
        services.AddSingleton(startupBrand);

        // Act
        services.AddDeploymentBrand(new ConfigurationBuilder().Build());
        services.AddEmailSender<SmtpEmailSender, SmtpSettings>();

        // Assert
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<DeploymentBrand>().Should().BeSameAs(startupBrand);
    }
}

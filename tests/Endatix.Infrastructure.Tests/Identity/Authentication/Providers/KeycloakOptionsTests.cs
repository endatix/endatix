using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Identity.Authentication.Providers;

public sealed class KeycloakOptionsTests
{
    private const string DEFAULT_TENANT_ID_KEY = "Endatix:Auth:Providers:Keycloak:DefaultTenantId";

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    public void StartupValidation_WhenEnabledWithoutDefaultTenantId_FailsNamingTheSetting(string? defaultTenantId)
    {
        // Arrange
        using var provider = BuildProvider(enabled: true, defaultTenantId);

        // Act
        var validate = () => provider.GetRequiredService<IStartupValidator>().Validate();

        // Assert
        validate.Should().Throw<OptionsValidationException>()
            .Which.Message.Should().Contain(DEFAULT_TENANT_ID_KEY);
    }

    [Fact]
    public void StartupValidation_WhenEnabledWithDefaultTenantId_Passes()
    {
        // Arrange
        using var provider = BuildProvider(enabled: true, defaultTenantId: "1");

        // Act
        var validate = () => provider.GetRequiredService<IStartupValidator>().Validate();

        // Assert
        validate.Should().NotThrow();
    }

    [Fact]
    public void StartupValidation_WhenDisabledWithoutDefaultTenantId_Passes()
    {
        // Arrange
        using var provider = BuildProvider(enabled: false, defaultTenantId: null);

        // Act
        var validate = () => provider.GetRequiredService<IStartupValidator>().Validate();

        // Assert
        validate.Should().NotThrow();
    }

    private static ServiceProvider BuildProvider(bool enabled, string? defaultTenantId)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Endatix:Auth:Providers:Keycloak:Enabled"] = enabled.ToString(),
            ["Endatix:Auth:Providers:Keycloak:Issuer"] = "http://localhost:8080/realms/endatix",
            ["Endatix:Auth:Providers:Keycloak:Audience"] = "endatix-hub",
            ["Endatix:Auth:Providers:Keycloak:ClientId"] = "endatix-hub",
            ["Endatix:Auth:Providers:Keycloak:ClientSecret"] = "client-secret",
            [DEFAULT_TENANT_ID_KEY] = defaultTenantId
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        new AuthProviderRegistry().RegisterProvider<KeycloakOptions>(new KeycloakAuthProvider(), services, configuration);

        return services.BuildServiceProvider();
    }
}

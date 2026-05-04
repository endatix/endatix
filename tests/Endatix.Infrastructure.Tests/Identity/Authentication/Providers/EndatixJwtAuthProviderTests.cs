using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Identity.Authentication.Providers;

public class EndatixUserJwtAuthProviderTests
{
    private readonly EndatixUserJwtAuthProvider _provider = new();
    private readonly AuthenticationBuilder _authBuilder;
    private readonly ServiceCollection _services = new();

    public EndatixUserJwtAuthProviderTests()
    {
        _authBuilder = _services.AddAuthentication();
    }

    [Fact]
    public void SchemeName_ShouldReturnCorrectValue()
    {
        _provider.SchemeName.Should().Be(AuthSchemes.EndatixJwt);
    }

    [Fact]
    public void CanHandle_WithMatchingIssuer_ShouldReturnTrue()
    {
        var issuer = "endatix-api";
        var config = CreateUserConfiguration(issuer, enabled: true);
        _provider.Configure(_authBuilder, config, false);

        _provider.CanHandle(issuer, "test-token").Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WithReBacIssuer_ShouldReturnFalse()
    {
        var config = CreateUserConfiguration("endatix-api", enabled: true);
        _provider.Configure(_authBuilder, config, false);

        _provider.CanHandle("edx_res_auth", "test-token").Should().BeFalse();
    }

    [Fact]
    public void CanHandle_WithNonMatchingIssuer_ShouldReturnFalse()
    {
        var configuredIssuer = "endatix-api";
        var config = CreateUserConfiguration(configuredIssuer, enabled: true);
        _provider.Configure(_authBuilder, config, false);

        _provider.CanHandle("different-issuer", "test-token").Should().BeFalse();
    }

    [Fact]
    public void CanHandle_WhenNotConfigured_ShouldReturnFalse()
    {
        _provider.CanHandle("endatix-api", "test-token").Should().BeFalse();
    }

    [Fact]
    public void Configure_WithValidConfiguration_ShouldReturnTrue()
    {
        var issuer = "endatix-api";
        var config = CreateUserConfiguration(issuer, enabled: true);

        var result = _provider.Configure(_authBuilder, config, false);

        result.Should().BeTrue();
        _provider.CanHandle(issuer, "test-token").Should().BeTrue();
    }

    [Fact]
    public void Configure_WithDevelopmentMode_ShouldConfigureCorrectly()
    {
        var issuer = "endatix-api";
        var config = CreateUserConfiguration(issuer, enabled: true);

        var result = _provider.Configure(_authBuilder, config, isDevelopment: true);

        result.Should().BeTrue();
        _provider.CanHandle(issuer, "test-token").Should().BeTrue();
    }

    [Fact]
    public void Configure_WithAllOptions_ShouldConfigureCorrectly()
    {
        var issuer = "endatix-api";
        var signingKey = "test-signing-key-32-characters-long";
        var audiences = new[] { "audience1", "audience2" };
        var config = CreateFullUserConfiguration(issuer, signingKey, audiences, enabled: true);

        var result = _provider.Configure(_authBuilder, config, false);

        result.Should().BeTrue();
        _provider.CanHandle(issuer, "test-token").Should().BeTrue();
    }

    [Fact]
    public void Configure_WhenDisabled_ShouldReturnFalse()
    {
        var issuer = "endatix-api";
        var config = CreateUserConfiguration(issuer, enabled: false);

        var result = _provider.Configure(_authBuilder, config, false);

        result.Should().BeFalse();
        _provider.CanHandle(issuer, "test-token").Should().BeFalse();
    }

    [Fact]
    public void Configure_WithNullConfiguration_ShouldThrowException()
    {
        IConfigurationSection? config = null;

        var action = () => _provider.Configure(_authBuilder, config!, false);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Configure_WithNullAuthenticationBuilder_ShouldThrowException()
    {
        var config = CreateUserConfiguration("endatix-api", enabled: true);
        AuthenticationBuilder? authBuilder = null;

        var action = () => _provider.Configure(authBuilder!, config, false);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Configure_WithEmptySpaceForIssuer_ShouldThrowException()
    {
        var config = CreateUserConfiguration(issuer: "   ", enabled: true);

        var action = () => _provider.Configure(_authBuilder, config, false);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EndatixJwtOptions_Constructor_ShouldSetDefaultIssuer()
    {
        var options = new EndatixJwtOptions();

        options.Issuer.Should().Be("endatix-api");
        options.ReBacIssuer.Should().Be("edx_res_auth");
        options.SchemeName.Should().Be(AuthSchemes.EndatixJwt);
        options.Audiences.Should().Contain("endatix-hub");
    }

    private static IConfigurationSection CreateUserConfiguration(string issuer, bool enabled)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Endatix:Auth:Providers:EndatixJwt:Issuer"] = issuer,
            ["Endatix:Auth:Providers:EndatixJwt:SigningKey"] = "test-signing-key-32-characters-long",
            ["Endatix:Auth:Providers:EndatixJwt:Audiences:0"] = "endatix-hub",
            ["Endatix:Auth:Providers:EndatixJwt:ReBacIssuer"] = "edx_res_auth",
            ["Endatix:Auth:Providers:EndatixJwt:Enabled"] = enabled.ToString().ToLowerInvariant(),
            ["Endatix:Auth:Providers:EndatixJwt:ValidateIssuer"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ValidateAudience"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ValidateLifetime"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ValidateIssuerSigningKey"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ClockSkewSeconds"] = "300",
            ["Endatix:Auth:Providers:EndatixJwt:MapInboundClaims"] = "false"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build()
            .GetSection("Endatix:Auth:Providers:EndatixJwt");
    }

    private static IConfigurationSection CreateFullUserConfiguration(string issuer, string signingKey, string[] audiences, bool enabled)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Endatix:Auth:Providers:EndatixJwt:Issuer"] = issuer,
            ["Endatix:Auth:Providers:EndatixJwt:SigningKey"] = signingKey,
            ["Endatix:Auth:Providers:EndatixJwt:ReBacIssuer"] = "edx_res_auth",
            ["Endatix:Auth:Providers:EndatixJwt:Enabled"] = enabled.ToString().ToLowerInvariant(),
            ["Endatix:Auth:Providers:EndatixJwt:ValidateIssuer"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ValidateAudience"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ValidateLifetime"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ValidateIssuerSigningKey"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ClockSkewSeconds"] = "300",
            ["Endatix:Auth:Providers:EndatixJwt:MapInboundClaims"] = "false"
        };

        for (var i = 0; i < audiences.Length; i++)
        {
            configData[$"Endatix:Auth:Providers:EndatixJwt:Audiences:{i}"] = audiences[i];
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build()
            .GetSection("Endatix:Auth:Providers:EndatixJwt");
    }
}

public class EndatixReBacJwtAuthProviderTests
{
    private readonly EndatixResourceJwtAuthProvider _provider = new();
    private readonly AuthenticationBuilder _authBuilder;
    private readonly ServiceCollection _services = new();

    public EndatixReBacJwtAuthProviderTests()
    {
        _authBuilder = _services.AddAuthentication();
    }

    [Fact]
    public void SchemeName_ShouldReturnEndatixReBac()
    {
        _provider.SchemeName.Should().Be(AuthSchemes.EndatixReBac);
    }

    [Fact]
    public void ConfigurationSectionPath_ShouldUseEndatixJwtSection()
    {
        _provider.ConfigurationSectionPath.Should().Be("Endatix:Auth:Providers:EndatixJwt");
    }

    [Fact]
    public void CanHandle_WithMatchingReBacIssuer_ShouldReturnTrue()
    {
        var rebacIssuer = "edx_res_auth";
        var config = CreateReBacConfiguration(rebacIssuer, enabled: true);
        _provider.Configure(_authBuilder, config, false);

        _provider.CanHandle(rebacIssuer, "test-token").Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WithHubIssuer_ShouldReturnFalse()
    {
        var config = CreateReBacConfiguration("edx_res_auth", enabled: true);
        _provider.Configure(_authBuilder, config, false);

        _provider.CanHandle("endatix-api", "test-token").Should().BeFalse();
    }

    [Fact]
    public void Configure_WhenDisabled_ShouldReturnFalse()
    {
        var config = CreateReBacConfiguration("edx_res_auth", enabled: false);

        var result = _provider.Configure(_authBuilder, config, false);

        result.Should().BeFalse();
    }

    private static IConfigurationSection CreateReBacConfiguration(string rebacIssuer, bool enabled)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Endatix:Auth:Providers:EndatixJwt:Issuer"] = "endatix-api",
            ["Endatix:Auth:Providers:EndatixJwt:SigningKey"] = "test-signing-key-32-characters-long",
            ["Endatix:Auth:Providers:EndatixJwt:Audiences:0"] = "endatix-hub",
            ["Endatix:Auth:Providers:EndatixJwt:ReBacIssuer"] = rebacIssuer,
            ["Endatix:Auth:Providers:EndatixJwt:Enabled"] = enabled.ToString().ToLowerInvariant(),
            ["Endatix:Auth:Providers:EndatixJwt:ValidateIssuer"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ValidateAudience"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ValidateLifetime"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ValidateIssuerSigningKey"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ClockSkewSeconds"] = "300",
            ["Endatix:Auth:Providers:EndatixJwt:MapInboundClaims"] = "false"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build()
            .GetSection("Endatix:Auth:Providers:EndatixJwt");
    }
}

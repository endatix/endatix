using Endatix.Core.Features.Auth;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Identity.Authentication;

public sealed class AuthProviderSettingsMapperTests
{
    [Fact]
    public void CreateBaseline_UsesConfiguredDisplayName()
    {
        AuthProviderOptions options = new TestAuthProviderOptions
        {
            Enabled = true,
            DisplayName = "Custom Provider",
        };

        var baseline = AuthProviderSettingsMapper.CreateBaseline(
            "CustomScheme",
            options,
            isRegistered: true,
            isActive: true);

        baseline.DisplayName.Should().Be("Custom Provider");
        baseline.IsEnabled.Should().BeTrue();
        baseline.IsActive.Should().BeTrue();
    }

    [Fact]
    public void CreateBaseline_FallsBackToProviderIdWhenDisplayNameMissing()
    {
        var baseline = AuthProviderSettingsMapper.CreateBaseline(
            "UnknownScheme",
            options: null,
            isRegistered: true,
            isActive: false);

        baseline.DisplayName.Should().Be("UnknownScheme");
        baseline.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void ApplyJwtFields_MapsIssuerAndAudiencesForUnknownJwtProvider()
    {
        TestJwtProviderOptions options = new()
        {
            Enabled = true,
            Issuer = "https://issuer.example",
            Audiences = ["audience-a", "audience-b"],
        };

        var baseline = AuthProviderSettingsMapper.CreateBaseline(
            "TestJwt",
            options,
            isRegistered: true,
            isActive: true);

        var mapped = AuthProviderSettingsMapper.ApplyJwtFields(baseline, options);

        mapped.Issuer.Should().Be("https://issuer.example");
        mapped.Audiences.Should().Equal("audience-a", "audience-b");
    }

    [Fact]
    public void ApplyJwtFields_UsesSingleAudiencePropertyForGoogleOptions()
    {
        GoogleOptions options = new()
        {
            Enabled = true,
            Audience = "google-client-id",
        };

        var baseline = AuthProviderSettingsMapper.CreateBaseline(
            "Google",
            options,
            isRegistered: true,
            isActive: true);

        var mapped = AuthProviderSettingsMapper.ApplyJwtFields(baseline, options);

        mapped.Audiences.Should().Equal("google-client-id");
    }

    private sealed class TestAuthProviderOptions : AuthProviderOptions;

    private sealed class TestJwtProviderOptions : JwtAuthProviderOptions;
}

public sealed class AuthSettingsReaderViewerTests
{
    [Fact]
    public void GetSettings_UsesViewerWhenProviderImplementsHook()
    {
        Dictionary<string, string?> config = new()
        {
            ["Endatix:Auth:Providers:ViewerTest:Enabled"] = "true",
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        var provider = Substitute.For<IAuthProvider, IAuthProviderSettingsViewer>();
        provider.SchemeName.Returns("ViewerTest");
        provider.CanHandle(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        provider.Configure(
                Arg.Any<Microsoft.AspNetCore.Authentication.AuthenticationBuilder>(),
                Arg.Any<IConfigurationSection>(),
                Arg.Any<bool>())
            .Returns(false);

        var viewer = (IAuthProviderSettingsViewer)provider;
        viewer
            .ViewSettings(
                Arg.Any<AuthProviderSettingsDto>(),
                Arg.Any<IConfigurationSection>(),
                Arg.Any<IList<string>>())
            .Returns(callInfo =>
            {
                var baseline = callInfo.ArgAt<AuthProviderSettingsDto>(0);

                return new AuthProviderSettingsDto
                {
                    ProviderId = baseline.ProviderId,
                    DisplayName = baseline.DisplayName,
                    IsRegistered = baseline.IsRegistered,
                    IsEnabled = baseline.IsEnabled,
                    IsActive = baseline.IsActive,
                    Issuer = "https://viewer.example",
                };
            });

        AuthProviderRegistry registry = new();
        ServiceCollection services = new();
        registry.RegisterProvider<TestJwtProviderOptions>(provider, services, configuration);

        AuthSettingsReader sut = new(registry, configuration);
        var settings = sut.GetSettings();

        var providerSettings = settings.Providers.Should().ContainSingle().Subject;
        providerSettings.ProviderId.Should().Be("ViewerTest");
        providerSettings.Issuer.Should().Be("https://viewer.example");
        viewer.Received(1).ViewSettings(
            Arg.Any<AuthProviderSettingsDto>(),
            Arg.Any<IConfigurationSection>(),
            Arg.Any<IList<string>>());
    }

    private sealed class TestJwtProviderOptions : JwtAuthProviderOptions;
}

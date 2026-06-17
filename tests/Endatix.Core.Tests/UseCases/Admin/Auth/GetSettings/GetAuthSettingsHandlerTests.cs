using Endatix.Core.Features.Auth;
using Endatix.Core.UseCases.Admin.Auth.GetSettings;

namespace Endatix.Core.Tests.UseCases.Admin.Auth.GetSettings;

public sealed class GetAuthSettingsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsSettingsFromReader()
    {
        AuthSettingsDto expected = new()
        {
            PlatformAdminRequiresLocalApproval = true,
            ConfigurationErrors = [],
            Providers =
            [
                new AuthProviderSettingsDto
                {
                    ProviderId = "EndatixJwt",
                    DisplayName = "Endatix JWT",
                    IsRegistered = true,
                    IsEnabled = true,
                    IsActive = true,
                    EndatixJwt = new EndatixJwtProviderDetailsDto
                    {
                        SigningKeyConfigured = true,
                        ReBacIssuer = "edx_res_auth",
                        FormAccessTokenExpiryMinutes = 60,
                    },
                },
            ],
        };

        var reader = Substitute.For<IAuthSettingsReader>();
        reader.GetSettings().Returns(expected);
        GetAuthSettingsHandler sut = new(reader);

        var result = await sut.Handle(new GetAuthSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Handle_WhenNoProviders_ReturnsSuccessWithEmptyProviders()
    {
        // Arrange
        AuthSettingsDto expected = new()
        {
            PlatformAdminRequiresLocalApproval = false,
            ConfigurationErrors = [],
            Providers = []
        };

        var reader = Substitute.For<IAuthSettingsReader>();
        reader.GetSettings().Returns(expected);
        GetAuthSettingsHandler sut = new(reader);

        // Act
        var result = await sut.Handle(new GetAuthSettingsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expected);
        result.Value.Providers.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenConfigurationErrorsExist_ReturnsSuccessWithErrors()
    {
        // Arrange
        AuthSettingsDto expected = new()
        {
            PlatformAdminRequiresLocalApproval = true,
            ConfigurationErrors = ["Keycloak:ClientId is missing", "EndatixJwt:SigningKey is missing"],
            Providers = []
        };

        var reader = Substitute.For<IAuthSettingsReader>();
        reader.GetSettings().Returns(expected);
        GetAuthSettingsHandler sut = new(reader);

        // Act
        var result = await sut.Handle(new GetAuthSettingsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ConfigurationErrors.Should().HaveCount(2);
        result.Value.ConfigurationErrors.Should().Contain(e => e.Contains("Keycloak"));
        result.Value.ConfigurationErrors.Should().Contain(e => e.Contains("EndatixJwt"));
    }

    [Fact]
    public async Task Handle_WithMultipleProviders_ReturnsAllProviders()
    {
        // Arrange
        AuthSettingsDto expected = new()
        {
            PlatformAdminRequiresLocalApproval = true,
            ConfigurationErrors = [],
            Providers =
            [
                new AuthProviderSettingsDto
                {
                    ProviderId = "EndatixJwt",
                    DisplayName = "Endatix JWT",
                    IsRegistered = true,
                    IsEnabled = true,
                    IsActive = true,
                    EndatixJwt = new EndatixJwtProviderDetailsDto
                    {
                        SigningKeyConfigured = true,
                        ReBacIssuer = "edx_res_auth",
                        FormAccessTokenExpiryMinutes = 60,
                    },
                },
                new AuthProviderSettingsDto
                {
                    ProviderId = "Keycloak",
                    DisplayName = "Keycloak",
                    IsRegistered = true,
                    IsEnabled = true,
                    IsActive = false,
                    Keycloak = new KeycloakProviderDetailsDto
                    {
                        ClientId = "endatix",
                        ClientSecretConfigured = true,
                        RoleMappingsConfigured = true,
                        RoleMappingCount = 3,
                        RejectDuplicateEmail = true,
                    },
                },
            ],
        };

        var reader = Substitute.For<IAuthSettingsReader>();
        reader.GetSettings().Returns(expected);
        GetAuthSettingsHandler sut = new(reader);

        // Act
        var result = await sut.Handle(new GetAuthSettingsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Providers.Should().HaveCount(2);
        result.Value.Providers.Should().Contain(p => p.ProviderId == "EndatixJwt");
        result.Value.Providers.Should().Contain(p => p.ProviderId == "Keycloak");
    }
}

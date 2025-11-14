using System.IdentityModel.Tokens.Jwt;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Tests;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Identity.Authentication;

public class JwtTokenServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly IAuthSchemeSelector _authSchemeSelector;

    public JwtTokenServiceTests()
    {
        _configuration = Substitute.For<IConfiguration>();
        _logger = new NullLogger<JwtTokenService>();
        _authSchemeSelector = Substitute.For<IAuthSchemeSelector>();
    }

    private readonly EndatixJwtOptions _validJwtOptions = new()
    {
        SigningKey = "validSigningKeyWithAtLeast32Characters",
        Issuer = "validIssuer",
        Audiences = new List<string> { "validAudience" },
        AccessExpiryInMinutes = 30,
        RefreshExpiryInDays = 7
    };

    [Fact]
    public void Constructor_NullSigningKey_ThrowsException()
    {
        // Arrange
        var invalidOptions = new EndatixJwtOptions
        {
            SigningKey = null!,
            Issuer = "validIssuer",
            Audiences = new List<string> { "validAudience" },
            AccessExpiryInMinutes = 30,
            RefreshExpiryInDays = 7
        };
        var optionsWrapper = Substitute.For<IOptions<EndatixJwtOptions>>();
        optionsWrapper.Value.Returns(invalidOptions);

        // Act
        var act = () => new JwtTokenService(optionsWrapper, _configuration, _logger, _authSchemeSelector);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithMessage(ErrorMessages.GetErrorMessage("SigningKey", ErrorType.SigningKeyEmpty));
    }

    [Fact]
    public void Constructor_EmptyIssuer_ThrowsException()
    {
        // Arrange
        var invalidOptions = new EndatixJwtOptions
        {
            SigningKey = "validSigningKeyWithAtLeast32Characters",
            Issuer = "",
            Audiences = new List<string> { "validAudience" },
            AccessExpiryInMinutes = 30,
            RefreshExpiryInDays = 7
        };
        var optionsWrapper = Substitute.For<IOptions<EndatixJwtOptions>>();
        optionsWrapper.Value.Returns(invalidOptions);

        // Act
        var act = () => new JwtTokenService(optionsWrapper, _configuration, _logger, _authSchemeSelector);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage("Issuer", ErrorType.IssuerEmpty));
    }

    [Fact]
    public void Constructor_EmptyAudiences_ThrowsException()
    {
        // Arrange
        var invalidOptions = new EndatixJwtOptions
        {
            SigningKey = "validSigningKeyWithAtLeast32Characters",
            Issuer = "validIssuer",
            Audiences = new List<string>(),
            AccessExpiryInMinutes = 30,
            RefreshExpiryInDays = 7
        };
        var optionsWrapper = Substitute.For<IOptions<EndatixJwtOptions>>();
        optionsWrapper.Value.Returns(invalidOptions);

        // Act
        var act = () => new JwtTokenService(optionsWrapper, _configuration, _logger, _authSchemeSelector);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage("Audiences", ErrorType.AudienceEmpty));
    }

    [Fact]
    public void Constructor_ZeroAccessExpiryInMinutes_ThrowsException()
    {
        // Arrange
        var invalidOptions = new EndatixJwtOptions
        {
            SigningKey = "validSigningKeyWithAtLeast32Characters",
            Issuer = "validIssuer",
            Audiences = new List<string> { "validAudience" },
            AccessExpiryInMinutes = 0,
            RefreshExpiryInDays = 7
        };
        var optionsWrapper = Substitute.For<IOptions<EndatixJwtOptions>>();
        optionsWrapper.Value.Returns(invalidOptions);

        // Act
        var act = () => new JwtTokenService(optionsWrapper, _configuration, _logger, _authSchemeSelector);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage("AccessExpiryInMinutes", ErrorType.AccessTokenZeroOrNegative));
    }

    [Fact]
    public void Constructor_ZeroRefreshExpiryInDays_ThrowsException()
    {
        // Arrange
        var invalidOptions = new EndatixJwtOptions
        {
            SigningKey = "validSigningKeyWithAtLeast32Characters",
            Issuer = "validIssuer",
            Audiences = new List<string> { "validAudience" },
            AccessExpiryInMinutes = 30,
            RefreshExpiryInDays = 0
        };
        var optionsWrapper = Substitute.For<IOptions<EndatixJwtOptions>>();
        optionsWrapper.Value.Returns(invalidOptions);

        // Act
        var act = () => new JwtTokenService(optionsWrapper, _configuration, _logger, _authSchemeSelector);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage("RefreshExpiryInDays", ErrorType.RefreshTokenZeroOrNegative));
    }

    [Fact]
    public void IssueAccessToken_ValidUser_ReturnsValidToken()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<EndatixJwtOptions>>();
        optionsWrapper.Value.Returns(_validJwtOptions);
        var tokenService = new JwtTokenService(optionsWrapper, _configuration, _logger, _authSchemeSelector);
        var user = new User(1, SampleData.TENANT_ID, "testuser", "test@example.com", false);

        // Act
        var result = tokenService.IssueAccessToken(user);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
        result.ExpireAt.Should().BeAfter(DateTime.UtcNow);

        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(result.Token) as JwtSecurityToken;
        jsonToken.Should().NotBeNull();
        jsonToken!.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == "1");
        jsonToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "test@example.com");
        jsonToken.Claims.Should().Contain(c => c.Type == ClaimNames.UserId && c.Value == "1");
        jsonToken.Claims.Should().Contain(c => c.Type == ClaimNames.TenantId && c.Value == SampleData.TENANT_ID.ToString());

        // Note: Role and Permission claims are now added by JwtClaimsTransformer during authentication,
        // not by JwtTokenService directly. This test only verifies the basic claims added by the token service.
    }

    [Fact]
    public async Task ValidateAccessToken_ValidToken_ReturnsSuccessWithUserId()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<EndatixJwtOptions>>();
        optionsWrapper.Value.Returns(_validJwtOptions);
        var tokenService = new JwtTokenService(optionsWrapper, _configuration, _logger, _authSchemeSelector);
        var user = new User(1, SampleData.TENANT_ID, "testuser", "test@example.com", false);
        var token = tokenService.IssueAccessToken(user);

        // Mock the auth scheme selector to return EndatixJwt for valid tokens
        _authSchemeSelector.SelectScheme(token.Token).Returns(AuthSchemes.EndatixJwt);

        // Act
        var result = await tokenService.ValidateAccessTokenAsync(token.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    [Fact]
    public async Task ValidateAccessToken_InvalidToken_ReturnsInvalidResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<EndatixJwtOptions>>();
        optionsWrapper.Value.Returns(_validJwtOptions);
        _authSchemeSelector.SelectScheme("invalidToken").Returns(AuthSchemes.EndatixJwt);
        var tokenService = new JwtTokenService(optionsWrapper, _configuration, _logger, _authSchemeSelector);

        // Act
        var result = await tokenService.ValidateAccessTokenAsync("invalidToken");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().NotBeNull();
        result.ValidationErrors.First().ErrorMessage.Should().Contain("JWT must have three segments");
    }

    [Fact]
    public async Task ValidateAccessToken_NonEndatixJwtToken_ReturnsNotSupportedError()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<EndatixJwtOptions>>();
        optionsWrapper.Value.Returns(_validJwtOptions);
        _authSchemeSelector.SelectScheme("someExternalToken").Returns("Keycloak");
        var tokenService = new JwtTokenService(optionsWrapper, _configuration, _logger, _authSchemeSelector);

        // Act
        var result = await tokenService.ValidateAccessTokenAsync("someExternalToken");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().NotBeNull();
        result.ValidationErrors.First().ErrorMessage.Should().Contain("Token validation not supported for scheme: Keycloak");
    }

    [Fact]
    public void IssueRefreshToken_Always_ReturnsValidToken()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<EndatixJwtOptions>>();
        optionsWrapper.Value.Returns(_validJwtOptions);
        var tokenService = new JwtTokenService(optionsWrapper, _configuration, _logger, _authSchemeSelector);

        // Act
        var result = tokenService.IssueRefreshToken();

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
        result.ExpireAt.Should().BeAfter(DateTime.UtcNow.AddDays(_validJwtOptions.RefreshExpiryInDays - 1));
        result.ExpireAt.Should().BeBefore(DateTime.UtcNow.AddDays(_validJwtOptions.RefreshExpiryInDays + 1));
    }

    [Fact]
    public async Task RevokeTokensAsync_NullUser_ReturnsNotFoundResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<EndatixJwtOptions>>();
        optionsWrapper.Value.Returns(_validJwtOptions);
        var tokenService = new JwtTokenService(optionsWrapper, _configuration, _logger, _authSchemeSelector);

        // Act
        var result = await tokenService.RevokeTokensAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task RevokeTokensAsync_ValidUser_ReturnsSuccessResult()
    {
        // Arrange
        var optionsWrapper = Substitute.For<IOptions<EndatixJwtOptions>>();
        optionsWrapper.Value.Returns(_validJwtOptions);
        var tokenService = new JwtTokenService(optionsWrapper, _configuration, _logger, _authSchemeSelector);
        var user = new User(1, SampleData.TENANT_ID, "testuser", "test@example.com", false);

        // Act
        var result = await tokenService.RevokeTokensAsync(user);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}

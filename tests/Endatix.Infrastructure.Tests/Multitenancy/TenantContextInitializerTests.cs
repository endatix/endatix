using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Multitenancy;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Endatix.Infrastructure.Tests.Multitenancy;

public sealed class TenantContextInitializerTests
{
    private const string EXTERNAL_ISSUER = "https://keycloak.test/realms/endatix";

    private readonly EndatixJwtOptions _jwtOptions = new();
    private readonly TenantContext _tenantContext = new();

    [Fact]
    public void TryInitialize_LocalJwtWithTenant_SetsTenantFromToken()
    {
        // Act
        var result = TryInitialize(CreatePrincipal(_jwtOptions.Issuer, tenantId: "3"));

        // Assert
        result.Should().Be(TenantInitializationResult.Set);
        _tenantContext.TenantId.Should().Be(3);
    }

    [Fact]
    public void TryInitialize_ReBacJwtWithTenant_SetsTenantFromToken()
    {
        // Act
        var result = TryInitialize(CreatePrincipal(_jwtOptions.ReBacIssuer, tenantId: "42"));

        // Assert
        result.Should().Be(TenantInitializationResult.Set);
        _tenantContext.TenantId.Should().Be(42);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("not-a-number")]
    public void TryInitialize_LocalJwtWithoutValidTenant_ReturnsNoTenant(string? tenantId)
    {
        // Act
        var result = TryInitialize(CreatePrincipal(_jwtOptions.Issuer, tenantId));

        // Assert
        result.Should().Be(TenantInitializationResult.NoTenant);
        _tenantContext.TenantId.Should().Be(0);
    }

    [Fact]
    public void TryInitialize_ExternalIssuerHydratedWithTenant_SetsTenantFromAuthorizedIdentity()
    {
        // Arrange
        var principal = CreatePrincipal(EXTERNAL_ISSUER, tenantId: null);
        principal.AddIdentity(CreateAuthorizedIdentity(tenantId: 7));

        // Act
        var result = TryInitialize(principal);

        // Assert
        result.Should().Be(TenantInitializationResult.Set);
        _tenantContext.TenantId.Should().Be(7);
    }

    [Fact]
    public void TryInitialize_ExternalIssuerHydratedWithTenant_IgnoresTheIssuersOwnTenantClaim()
    {
        // Arrange
        var principal = CreatePrincipal(EXTERNAL_ISSUER, tenantId: "5");
        principal.AddIdentity(CreateAuthorizedIdentity(tenantId: 7));

        // Act
        TryInitialize(principal);

        // Assert
        _tenantContext.TenantId.Should().Be(7);
    }

    [Fact]
    public void TryInitialize_ExternalIssuerHydratedAtTenantZero_ReturnsNoTenant()
    {
        // Arrange
        var principal = CreatePrincipal(EXTERNAL_ISSUER, tenantId: "5");
        principal.AddIdentity(CreateAuthorizedIdentity(tenantId: 0));

        // Act
        var result = TryInitialize(principal);

        // Assert
        result.Should().Be(TenantInitializationResult.NoTenant);
        _tenantContext.TenantId.Should().Be(0);
    }

    [Fact]
    public void TryInitialize_ExternalIssuerNotHydrated_ReturnsNoTenant()
    {
        // Act
        var result = TryInitialize(CreatePrincipal(EXTERNAL_ISSUER, tenantId: "5"));

        // Assert
        result.Should().Be(TenantInitializationResult.NoTenant);
        _tenantContext.TenantId.Should().Be(0);
    }

    [Fact]
    public void TryInitialize_UnexpectedTenantContext_ReturnsNotWritable()
    {
        // Arrange
        var unexpectedTenantContext = Substitute.For<ITenantContext>();

        // Act
        var result = TenantContextInitializer.TryInitialize(
            unexpectedTenantContext,
            CreatePrincipal(_jwtOptions.Issuer, tenantId: "3"),
            _jwtOptions);

        // Assert
        result.Should().Be(TenantInitializationResult.NotWritable);
    }

    private TenantInitializationResult TryInitialize(ClaimsPrincipal user) =>
        TenantContextInitializer.TryInitialize(_tenantContext, user, _jwtOptions);

    private static ClaimsPrincipal CreatePrincipal(string issuer, string? tenantId)
    {
        var identity = new ClaimsIdentity(authenticationType: "Bearer");
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Iss, issuer));
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, "123"));
        if (tenantId is not null)
        {
            identity.AddClaim(new Claim(ClaimNames.TenantId, tenantId));
        }

        return new ClaimsPrincipal(identity);
    }

    private static AuthorizedIdentity CreateAuthorizedIdentity(long tenantId) =>
        new(AuthorizationData.ForAuthenticatedUser(
            userId: "123",
            tenantId: tenantId,
            roles: [],
            permissions: []));
}

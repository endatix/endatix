using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Features.Authorization.PublicForm;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Endatix.Infrastructure.Tests.Features.Authorization.PublicForm;

/// <summary>
/// The form access token carries its grant: a token issued for a private form says so, and a token issued before
/// the grant claim existed counts as a public grant.
/// </summary>
public sealed class FormAccessTokenServiceTests
{
    private const long FORM_ID = 12;
    private const long TENANT_ID = 3;
    private const string SIGNING_KEY = "form-access-token-service-tests-signing-key-0123456789";

    private readonly EndatixJwtOptions _options = new() { SigningKey = SIGNING_KEY };
    private readonly FormAccessTokenService _sut;

    public FormAccessTokenServiceTests()
    {
        var snapshot = Substitute.For<IOptionsSnapshot<EndatixJwtOptions>>();
        snapshot.Value.Returns(_options);
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        _sut = new FormAccessTokenService(snapshot, dateTimeProvider);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_grant_survives_a_round_trip(bool isPublicGrant)
    {
        var token = _sut.CreateToken(FORM_ID, TENANT_ID, isPublicGrant).Value.Token;

        var claims = _sut.ValidateToken(token);

        claims.IsSuccess.Should().BeTrue();
        claims.Value.FormId.Should().Be(FORM_ID);
        claims.Value.TenantId.Should().Be(TENANT_ID);
        claims.Value.IsPublicGrant.Should().Be(isPublicGrant);
    }

    [Fact]
    public void A_token_without_a_grant_claim_counts_as_a_public_grant()
    {
        var token = LegacyToken();

        var claims = _sut.ValidateToken(token);

        claims.IsSuccess.Should().BeTrue();
        claims.Value.FormId.Should().Be(FORM_ID);
        claims.Value.IsPublicGrant.Should().BeTrue();
    }

    /// <summary>A token as issued before the grant claim: same issuer, audience and key, no <c>grant</c>.</summary>
    private string LegacyToken()
    {
        Claim[] claims =
        [
            new(JwtReBacClaims.RESOURCE_TYPE, JwtReBacClaims.RESOURCE_TYPE_VALUE_FORM),
            new(JwtReBacClaims.RESOURCE_ID, FORM_ID.ToString()),
            new(ClaimNames.TenantId, TENANT_ID.ToString()),
        ];
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SIGNING_KEY));
        JwtSecurityToken token = new(
            issuer: _options.ReBacIssuer,
            audience: _options.Audiences[0],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

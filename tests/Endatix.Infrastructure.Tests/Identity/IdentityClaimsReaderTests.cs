using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Endatix.Infrastructure.Identity;

namespace Endatix.Infrastructure.Tests.Identity.Provisioning;

public sealed class IdentityClaimsReaderTests
{
    [Fact]
    public void FromClaimsPrincipal_WithShortJwtClaims_ReturnsExternalIdentityProfile()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Email, " test@example.com "),
            new Claim("preferred_username", "1234"),
            new Claim("name", "first last")
        ]));

        var profile = IdentityClaimsReader.FromClaimsPrincipal(principal);

        profile.Email.Should().Be("test@example.com");
        profile.DisplayName.Should().Be("first last");
    }

    [Fact]
    public void FromClaimsPrincipal_WithMappedClaimTypes_ReturnsExternalIdentityProfile()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Email, "mapped@example.com"),
            new Claim(ClaimTypes.GivenName, "first"),
            new Claim(ClaimTypes.Surname, "last")
        ]));

        var profile = IdentityClaimsReader.FromClaimsPrincipal(principal);

        profile.Email.Should().Be("mapped@example.com");
        profile.DisplayName.Should().Be("first last");
    }

    [Fact]
    public void FromClaimsPrincipal_WithPreferredUsernameOnly_UsesItAsDisplayName()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Email, "mapped@example.com"),
            new Claim("preferred_username", "preferred-user")
        ]));

        var profile = IdentityClaimsReader.FromClaimsPrincipal(principal);

        profile.Email.Should().Be("mapped@example.com");
        profile.DisplayName.Should().Be("preferred-user");
    }
}

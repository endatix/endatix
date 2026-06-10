using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Endatix.Infrastructure.Identity.Provisioning;

namespace Endatix.Infrastructure.Tests.Identity.Provisioning;

public sealed class ExternalIdentityClaimReaderTests
{
    [Fact]
    public void FromClaimsPrincipal_WithShortJwtClaims_ReturnsExternalIdentityProfile()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Email, " test@example.com "),
            new Claim("email_verified", "false"),
            new Claim("preferred_username", "1234"),
            new Claim("name", "first last")
        ]));

        var profile = ExternalIdentityClaimReader.FromClaimsPrincipal(principal);

        profile.Email.Should().Be("test@example.com");
        profile.EmailVerified.Should().BeFalse();
        profile.PreferredUsername.Should().Be("1234");
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

        var profile = ExternalIdentityClaimReader.FromClaimsPrincipal(principal);

        profile.Email.Should().Be("mapped@example.com");
        profile.DisplayName.Should().Be("first last");
    }

}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Endatix.Infrastructure.Features.Submitters;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Features.Submitters;

public sealed class EndatixSubmitterClaimExtractorTests
{
    private const string EndatixIssuer = "endatix-api";
    private readonly EndatixSubmitterClaimExtractor _extractor = new(
        CreateRegistry(AuthProviders.Endatix, EndatixIssuer),
        Options.Create(new SubmitterOptions()),
        new SubmitterClaimReader());

    [Fact]
    public void Extract_WithNativeLongSubject_ReturnsAppUserId()
    {
        var principal = CreateAuthenticatedPrincipal(
        [
            new Claim(ClaimNames.UserId, "123456789"),
            new Claim(JwtRegisteredClaimNames.Iss, EndatixIssuer),
            new Claim("preferred_username", "operator@example.com")
        ]);

        var input = _extractor.Extract(principal);

        input.AuthProvider.Should().Be(AuthProviders.Endatix);
        input.ExternalSubjectId.Should().BeNull();
        input.AppUserId.Should().Be(123456789);
        input.DisplayId.Should().Be("123456789");
    }

    [Fact]
    public void Extract_WithInvalidSubject_ThrowsClearError()
    {
        var principal = CreateAuthenticatedPrincipal(
        [
            new Claim(ClaimNames.UserId, "not-a-long"),
            new Claim(JwtRegisteredClaimNames.Iss, EndatixIssuer)
        ]);

        var act = () => _extractor.Extract(principal);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Failed to parse Endatix subject 'not-a-long' as long. CanExtract should have prevented this.");
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(IEnumerable<Claim> claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Endatix"));
    }

    private static AuthProviderRegistry CreateRegistry(string schemeName, string issuer)
    {
        AuthProviderRegistry registry = new();
        TestAuthProvider provider = new(schemeName, issuer);
        registry.RegisterProvider<TestAuthProviderOptions>(
            provider,
            new ServiceCollection(),
            new ConfigurationBuilder().Build());
        registry.AddActiveProvider(provider);

        return registry;
    }

    private sealed class TestAuthProvider(string schemeName, string issuer) : IAuthProvider
    {
        public string SchemeName => schemeName;

        public bool CanHandle(string tokenIssuer, string rawToken) =>
            string.Equals(tokenIssuer, issuer, StringComparison.Ordinal);

        public bool Configure(AuthenticationBuilder builder, IConfigurationSection providerConfig, bool isDevelopment = false) => true;
    }

    private sealed class TestAuthProviderOptions : AuthProviderOptions
    {
    }
}

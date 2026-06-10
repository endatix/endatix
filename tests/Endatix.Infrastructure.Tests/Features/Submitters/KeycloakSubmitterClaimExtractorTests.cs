using System.Security.Claims;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Features.Submitters;
using Endatix.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Features.Submitters;

public sealed class KeycloakSubmitterClaimExtractorTests
{
    private readonly KeycloakSubmitterClaimExtractor _extractor = CreateExtractor(new SubmitterOptions());

    [Fact]
    public void CanExtract_WithHydratedKeycloakOperatorPrincipal_UsesRawExternalSubject()
    {
        const string externalSubjectId = "0f6d8b28-e761-4033-8e84-2ddebcec49ce";
        var principal = CreateAuthenticatedPrincipal(
        [
            new Claim(ClaimNames.EndatixUserId, "123456789"),
            new Claim(ClaimNames.UserId, externalSubjectId),
            new Claim("preferred_username", "operator@example.com")
        ]);

        var canExtract = _extractor.CanExtract(principal);
        var input = _extractor.Extract(principal);

        canExtract.Should().BeTrue();
        input.AuthProvider.Should().Be(AuthProviders.Keycloak);
        input.ExternalSubjectId.Should().Be(externalSubjectId);
        input.AppUserId.Should().BeNull();
        input.DisplayId.Should().Be("operator@example.com");
    }

    [Fact]
    public void CanExtract_WithMappedNameIdentifierGuid_ReturnsTrue()
    {
        const string externalSubjectId = "bf89d22f-acbc-4574-bf7d-53dbcf438bb7";
        var principal = CreateAuthenticatedPrincipal(
        [
            new Claim(ClaimTypes.NameIdentifier, externalSubjectId),
            new Claim("panelistId", "1234")
        ]);

        var canExtract = _extractor.CanExtract(principal);
        var input = _extractor.Extract(principal);

        canExtract.Should().BeTrue();
        input.ExternalSubjectId.Should().Be(externalSubjectId);
        input.DisplayId.Should().Be("1234");
    }

    [Fact]
    public void CanExtract_WithNativeLongSubject_ReturnsFalse()
    {
        var principal = CreateAuthenticatedPrincipal(
        [
            new Claim(ClaimNames.UserId, "123456789")
        ]);

        var canExtract = _extractor.CanExtract(principal);

        canExtract.Should().BeFalse();
    }

    [Fact]
    public void Extract_WithConfiguredDisplayIdPriority_UsesFirstConfiguredClaim()
    {
        var extractor = CreateExtractor(new SubmitterOptions
        {
            DisplayIdClaimTypes = ["employee_number", "preferred_username"]
        });
        var principal = CreateAuthenticatedPrincipal(
        [
            new Claim(ClaimNames.UserId, "bf89d22f-acbc-4574-bf7d-53dbcf438bb7"),
            new Claim("employee_number", "E-123"),
            new Claim("preferred_username", "panelist@example.com")
        ]);

        var input = extractor.Extract(principal);

        input.DisplayId.Should().Be("E-123");
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(IEnumerable<Claim> claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Keycloak"));
    }

    private static KeycloakSubmitterClaimExtractor CreateExtractor(SubmitterOptions options)
    {
        return new KeycloakSubmitterClaimExtractor(Options.Create(options), new SubmitterClaimReader());
    }
}

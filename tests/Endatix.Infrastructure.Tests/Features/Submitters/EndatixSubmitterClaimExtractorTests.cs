using System.Security.Claims;
using Endatix.Infrastructure.Features.Submitters;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Features.Submitters;

public sealed class EndatixSubmitterClaimExtractorTests
{
    private readonly EndatixSubmitterClaimExtractor _extractor = new(
        Options.Create(new SubmitterOptions()),
        new SubmitterClaimReader());

    [Fact]
    public void Extract_WithNativeLongSubject_ReturnsAppUserId()
    {
        // Arrange
        var principal = CreateAuthenticatedPrincipal(
        [
            new Claim(ClaimNames.UserId, "123456789"),
            new Claim("preferred_username", "operator@example.com")
        ]);

        // Act
        var input = _extractor.Extract(principal);

        // Assert
        input.AuthProvider.Should().Be(AuthProviders.Endatix);
        input.ExternalSubjectId.Should().BeNull();
        input.AppUserId.Should().Be(123456789);
        input.DisplayId.Should().Be("operator@example.com");
    }

    [Fact]
    public void Extract_WithInvalidSubject_ThrowsClearError()
    {
        // Arrange
        var principal = CreateAuthenticatedPrincipal(
        [
            new Claim(ClaimNames.UserId, "not-a-long")
        ]);

        // Act
        var act = () => _extractor.Extract(principal);

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Failed to parse Endatix subject 'not-a-long' as long. CanExtract should have prevented this.");
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(IEnumerable<Claim> claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Endatix"));
    }
}

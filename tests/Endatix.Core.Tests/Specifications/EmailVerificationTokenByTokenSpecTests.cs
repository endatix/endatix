using Ardalis.Specification;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Specifications;

namespace Endatix.Core.Tests.Specifications;

public class EmailVerificationTokenByTokenSpecTests
{
    private const string RawToken = "hLQ2m9xTf0sVrN7pKd4eYb1uAg6ZjC3o";
    private const long USER_ID = 4242;

    [Fact]
    public void Constructor_RawToken_MatchesTheStoredHash()
    {
        // Arrange
        var stored = ExistingToken();
        var spec = new EmailVerificationTokenByTokenSpec(RawToken);

        // Act
        var matches = Matches(spec, stored);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact]
    public void Constructor_PaddedRawToken_MatchesTheStoredHash()
    {
        // Arrange - links pasted out of an email client arrive with stray whitespace.
        var stored = ExistingToken();
        var spec = new EmailVerificationTokenByTokenSpec($"  {RawToken}\n");

        // Act
        var matches = Matches(spec, stored);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact]
    public void Constructor_StoredHashPresentedAsToken_DoesNotMatch()
    {
        // Arrange - the column holds a hash precisely so that read access to the table is not read
        // access to the tokens. Accepting the stored value verbatim would make it replayable, and
        // anyone who could read a backup or a replica could verify any pending account.
        var stored = ExistingToken();
        var spec = new EmailVerificationTokenByTokenSpec(stored.Token);

        // Act
        var matches = Matches(spec, stored);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact]
    public void Constructor_UnrelatedToken_DoesNotMatch()
    {
        // Arrange
        var stored = ExistingToken();
        var spec = new EmailVerificationTokenByTokenSpec("a-different-token");

        // Act
        var matches = Matches(spec, stored);

        // Assert
        matches.Should().BeFalse();
    }

    private static EmailVerificationToken ExistingToken() =>
        new(USER_ID, RawToken, DateTime.UtcNow.AddHours(1));

    private static bool Matches(
        ISpecification<EmailVerificationToken> spec,
        EmailVerificationToken token) =>
        spec.WhereExpressions.All(where => where.FilterFunc(token));
}

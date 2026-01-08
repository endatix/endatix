using Endatix.Core.UseCases.Submissions.GetByAccessToken;

namespace Endatix.Core.Tests.UseCases.Submissions.GetByAccessToken;

public class GetByAccessTokenQueryTests
{
    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 123L;
        var token = "12345.1234567890.rw.signature";

        // Act
        var query = new GetByAccessTokenQuery(formId, token);

        // Assert
        query.FormId.Should().Be(formId);
        query.Token.Should().Be(token);
    }

    [Fact]
    public void Constructor_ZeroFormId_ThrowsArgumentException()
    {
        // Act
        var act = () => new GetByAccessTokenQuery(0, "token");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NegativeFormId_ThrowsArgumentException()
    {
        // Act
        var act = () => new GetByAccessTokenQuery(-1, "token");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullToken_ThrowsArgumentException()
    {
        // Act
        var act = () => new GetByAccessTokenQuery(123L, null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmptyToken_ThrowsArgumentException()
    {
        // Act
        var act = () => new GetByAccessTokenQuery(123L, "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}

using Endatix.Core.UseCases.Submissions.GetByToken;
using FluentAssertions;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Submissions.GetByToken;

public class GetByTokenQueryTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormId_ThrowsArgumentException()
    {
        // Arrange
        var formId = -1;
        var token = "valid-token";

        // Act
        Action act = () => new GetByTokenQuery(formId, token);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(formId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_NullToken_ThrowsArgumentException()
    {
        // Arrange
        var formId = 1;
        string? token = null;

        // Act
        Action act = () => new GetByTokenQuery(formId, token!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(token), Null));
    }

    [Fact]
    public void Constructor_EmptyToken_ThrowsArgumentException()
    {
        // Arrange
        var formId = 1;
        var token = string.Empty;

        // Act
        Action act = () => new GetByTokenQuery(formId, token);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(token), Empty));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 1;
        var token = "valid-token";

        // Act
        var query = new GetByTokenQuery(formId, token);

        // Assert
        query.FormId.Should().Be(formId);
        query.Token.Should().Be(token);
    }
}

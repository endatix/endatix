using Endatix.Core.UseCases.FormDefinitions.GetActive;
using FluentAssertions;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.GetActive;

public class GetActiveFormDefinitionQueryTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormId_ThrowsArgumentException()
    {
        // Arrange
        var formId = -1;

        // Act
        Action act = () => new GetActiveFormDefinitionQuery(formId, null, "forms.view");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(formId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 1;
        var userId = "123";
        var requiredPermission = "forms.view";

        // Act
        var query = new GetActiveFormDefinitionQuery(formId, userId, requiredPermission);

        // Assert
        query.FormId.Should().Be(formId);
        query.UserId.Should().Be(userId);
        query.RequiredPermission.Should().Be(requiredPermission);
    }
}

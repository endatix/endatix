using Endatix.Api.Endpoints.Access;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Access;

public class GetFormTemplateAccessValidatorTests
{
    private readonly GetFormTemplateAccessValidator _validator = new();

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(999999)]
    public void Validate_ValidTemplateId_PassesValidation(long templateId)
    {
        // Arrange
        var request = new GetFormTemplateAccessRequest { TemplateId = templateId };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.TemplateId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_InvalidTemplateId_ReturnsError(long templateId)
    {
        // Arrange
        var request = new GetFormTemplateAccessRequest { TemplateId = templateId };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TemplateId)
            .WithErrorMessage("'Template Id' must be greater than '0'.");
    }

    [Fact]
    public void Validate_MinimalValidRequest_PassesValidation()
    {
        // Arrange
        var request = new GetFormTemplateAccessRequest { TemplateId = 1 };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}

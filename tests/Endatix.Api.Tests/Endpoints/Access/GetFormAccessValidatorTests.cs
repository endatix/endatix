using Endatix.Api.Endpoints.Access;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Access;

public class GetFormAccessValidatorTests
{
    private readonly GetFormAccessValidator _validator = new();

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(999999)]
    public void Validate_ValidFormId_PassesValidation(long formId)
    {
        // Arrange
        var request = new GetFormAccessRequest { FormId = formId };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.FormId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_InvalidFormId_ReturnsError(long formId)
    {
        // Arrange
        var request = new GetFormAccessRequest { FormId = formId };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FormId)
            .WithErrorMessage("'Form Id' must be greater than '0'.");
    }

    [Fact]
    public void Validate_MinimalValidRequest_PassesValidation()
    {
        // Arrange
        var request = new GetFormAccessRequest { FormId = 1 };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_LargeFormId_PassesValidation()
    {
        // Arrange
        var request = new GetFormAccessRequest { FormId = long.MaxValue };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.FormId);
    }

    [Fact]
    public void Validate_One_PassesValidation()
    {
        // Arrange
        var request = new GetFormAccessRequest { FormId = 1 };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.FormId);
    }
}

using Endatix.Api.Endpoints.Access;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Access;

public class GetSubmissionAccessValidatorTests
{
    private readonly GetSubmissionAccessValidator _validator = new();

    [Theory]
    [InlineData(1, 1)]
    [InlineData(100, 200)]
    [InlineData(999999, 888888)]
    public void Validate_ValidIds_PassesValidation(long formId, long submissionId)
    {
        // Arrange
        var request = new GetSubmissionAccessRequest { FormId = formId, SubmissionId = submissionId };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.FormId);
        result.ShouldNotHaveValidationErrorFor(x => x.SubmissionId);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(-100, 1)]
    public void Validate_InvalidFormId_ReturnsError(long formId, long submissionId)
    {
        // Arrange
        var request = new GetSubmissionAccessRequest { FormId = formId, SubmissionId = submissionId };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FormId)
            .WithErrorMessage("'Form Id' must be greater than '0'.");
        result.ShouldNotHaveValidationErrorFor(x => x.SubmissionId);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, -100)]
    public void Validate_InvalidSubmissionId_ReturnsError(long formId, long submissionId)
    {
        // Arrange
        var request = new GetSubmissionAccessRequest { FormId = formId, SubmissionId = submissionId };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.FormId);
        result.ShouldHaveValidationErrorFor(x => x.SubmissionId)
            .WithErrorMessage("'Submission Id' must be greater than '0'.");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, -1)]
    [InlineData(0, -1)]
    [InlineData(-1, 0)]
    public void Validate_BothInvalid_ReturnsBothErrors(long formId, long submissionId)
    {
        // Arrange
        var request = new GetSubmissionAccessRequest { FormId = formId, SubmissionId = submissionId };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FormId);
        result.ShouldHaveValidationErrorFor(x => x.SubmissionId);
    }

    [Fact]
    public void Validate_MinimalValidRequest_PassesValidation()
    {
        // Arrange
        var request = new GetSubmissionAccessRequest { FormId = 1, SubmissionId = 1 };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}

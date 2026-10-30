using Endatix.Api.Endpoints.Submissions;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public sealed class SetSubmissionTestValidatorTests
{
    private readonly SetSubmissionTestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        // Act
        var result = _validator.TestValidate(new SetSubmissionTestRequest { FormId = 1, SubmissionId = 2, IsTest = false });

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_MissingIsTest_Fails()
    {
        // Act
        var result = _validator.TestValidate(new SetSubmissionTestRequest { FormId = 1, SubmissionId = 2 });

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.IsTest);
    }

    [Fact]
    public void Validate_ZeroIds_Fail()
    {
        // Act
        var result = _validator.TestValidate(new SetSubmissionTestRequest { IsTest = true });

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FormId);
        result.ShouldHaveValidationErrorFor(x => x.SubmissionId);
    }
}

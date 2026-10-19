using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.Abstractions.Submitters;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public sealed class CreateOnBehalfValidatorTests
{
    private readonly CreateSubmissionOnBehalfValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_SubmitterWithOnlyASubject_Passes(string? displayId)
    {
        CreateSubmissionOnBehalfRequest request = new()
        {
            FormId = 1,
            Submitter = new SubmitterInput("panel-42", displayId),
        };

        var result = await _validator.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_SubmitterWithoutASubject_Fails(string subject)
    {
        CreateSubmissionOnBehalfRequest request = new()
        {
            FormId = 1,
            Submitter = new SubmitterInput(subject, "Jane D."),
        };

        var result = await _validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Submitter!.ExternalSubjectId);
    }
}

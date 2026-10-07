using Endatix.Api.Endpoints.Submissions;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class PartialUpdateSubmissionByTokenValidatorTests
{
    private readonly PartialUpdateSubmissionByTokenValidator _validator = new();

    private static PartialUpdateSubmissionByTokenRequest Request(string? collectionOutcome) =>
        new()
        {
            SubmissionToken = "token",
            FormId = 1,
            CollectionOutcome = collectionOutcome
        };

    [Theory]
    [InlineData(null)]
    [InlineData("screen_out")]
    public void Validate_SupportedCollectionOutcome_Passes(string? collectionOutcome)
    {
        var result = _validator.TestValidate(Request(collectionOutcome));

        result.ShouldNotHaveValidationErrorFor(x => x.CollectionOutcome);
    }

    [Theory]
    [InlineData("screenout")]
    [InlineData("SCREEN_OUT")]
    [InlineData("complete")]
    public void Validate_UnsupportedCollectionOutcome_Fails(string collectionOutcome)
    {
        var result = _validator.TestValidate(Request(collectionOutcome));

        result.ShouldHaveValidationErrorFor(x => x.CollectionOutcome);
    }
}

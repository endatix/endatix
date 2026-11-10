using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.Entities;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public sealed class PartialUpdateByAccessTokenValidatorTests
{
    private readonly PartialUpdateByAccessTokenValidator _validator = new();

    private static PartialUpdateByAccessTokenRequest Request(string? collectionOutcome) =>
        new()
        {
            Token = "token",
            FormId = 1,
            CollectionOutcome = collectionOutcome
        };

    [Theory]
    [InlineData(null)]
    [InlineData(CollectionStatusCodes.ScreenOut)]
    public void Validate_RespondentCollectionOutcome_Passes(string? collectionOutcome)
    {
        // Arrange
        var request = Request(collectionOutcome);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.CollectionOutcome);
    }

    [Fact]
    public void Validate_QuotaFullCollectionOutcome_FailsAsServerOnly()
    {
        // Arrange
        var request = Request(CollectionStatusCodes.QuotaFull);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CollectionOutcome)
            .WithErrorMessage("'quota_full' is set by the server; a respondent save can't set it.");
    }
}

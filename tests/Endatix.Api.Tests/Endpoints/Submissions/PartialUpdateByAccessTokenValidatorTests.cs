using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Submissions;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class PartialUpdateByAccessTokenValidatorTests
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

    [Theory]
    [InlineData(CollectionStatusCodes.QuotaFull)]
    [InlineData(CollectionStatusCodes.Cancelled)]
    public void Validate_ServerOnlyCollectionOutcome_FailsAsServerOnly(string collectionOutcome)
    {
        // Arrange
        var request = Request(collectionOutcome);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CollectionOutcome)
            .WithErrorMessage(CollectionOutcomes.SERVER_ONLY_MESSAGE);
    }
}

using Endatix.Api.Endpoints.Submissions;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class CreateAccessTokenValidatorTests
{
    private readonly CreateAccessTokenValidator _validator = new();

    private static CreateAccessTokenRequest ValidRequest(int? expiryMinutes = 60) =>
        new()
        {
            FormId = 1,
            SubmissionId = 42,
            ExpiryMinutes = expiryMinutes,
            Permissions = ["view"]
        };

    [Fact]
    public void Validate_MaxExpiryMinutes_IsSixtyDays()
    {
        CreateAccessTokenValidator.MaxExpiryMinutes.Should().Be(86_400);
    }

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var result = _validator.TestValidate(ValidRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ExpiryAtMax_Passes()
    {
        var result = _validator.TestValidate(ValidRequest(CreateAccessTokenValidator.MaxExpiryMinutes));

        result.ShouldNotHaveValidationErrorFor(x => x.ExpiryMinutes);
    }

    [Fact]
    public void Validate_ExpiryAboveMax_Fails()
    {
        var result = _validator.TestValidate(ValidRequest(CreateAccessTokenValidator.MaxExpiryMinutes + 1));

        result.ShouldHaveValidationErrorFor(x => x.ExpiryMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveExpiry_Fails(int expiryMinutes)
    {
        var result = _validator.TestValidate(ValidRequest(expiryMinutes));

        result.ShouldHaveValidationErrorFor(x => x.ExpiryMinutes);
    }

    [Fact]
    public void Validate_MissingExpiry_Fails()
    {
        var result = _validator.TestValidate(ValidRequest(null));

        result.ShouldHaveValidationErrorFor(x => x.ExpiryMinutes);
    }
}

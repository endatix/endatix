using Endatix.Api.Endpoints.Auth;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Auth;

public class RefreshTokenValidatorTests
{
    private readonly RefreshTokenValidator _validator = new();

    [Fact]
    public void Validate_MissingAuthorization_ReportsOnlyNotEmpty()
    {
        // Arrange
        var request = new RefreshTokenRequest(null, "refresh");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Authorization)
            .WithErrorCode("NotEmptyValidator")
            .Only();
    }

    [Fact]
    public void Validate_AuthorizationWithoutBearer_ReportsBearerRule()
    {
        // Arrange
        var request = new RefreshTokenRequest("Basic abc", "refresh");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Authorization)
            .WithErrorMessage("Authorization header must start with 'Bearer '.");
    }
}

using Endatix.Api.Endpoints.Access;
using Endatix.Core.Authorization.Access;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Access;

public class GetFormPublicAccessValidatorTests
{
    private readonly GetFormPublicAccessValidator _validator = new();

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(999999)]
    public void Validate_ValidFormIdOnly_PassesValidation(long formId)
    {
        // Arrange
        var request = new GetFormPublicAccessRequest { FormId = formId };

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
        var request = new GetFormPublicAccessRequest { FormId = formId };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FormId)
            .WithErrorMessage("'Form Id' must be greater than '0'.");
    }

    [Fact]
    public void Validate_TokenWithNullTokenType_ReturnsError()
    {
        // Arrange
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1,
            Token = "some-token",
            TokenType = null
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TokenType)
            .WithErrorMessage("'Token Type' must not be empty.");
    }

    [Fact]
    public void Validate_TokenWithAccessTokenType_PassesValidation()
    {
        // Arrange
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1,
            Token = "access-token",
            TokenType = SubmissionTokenType.AccessToken
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Token);
        result.ShouldNotHaveValidationErrorFor(x => x.TokenType);
    }

    [Fact]
    public void Validate_TokenWithSubmissionTokenType_PassesValidation()
    {
        // Arrange
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1,
            Token = "submission-token",
            TokenType = SubmissionTokenType.SubmissionToken
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Token);
        result.ShouldNotHaveValidationErrorFor(x => x.TokenType);
    }

    [Fact]
    public void Validate_NoToken_PassesValidation()
    {
        // Arrange
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1,
            Token = null,
            TokenType = null
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_TokenTypeWithNoToken_ReturnsError()
    {
        // Arrange
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1,
            Token = null,
            TokenType = SubmissionTokenType.AccessToken
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TokenType)
            .WithErrorMessage("Token must be provided when Token Type is specified.");
    }

    [Fact]
    public void Validate_TokenTypeWithEmptyToken_ReturnsError()
    {
        // Arrange
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1,
            Token = string.Empty,
            TokenType = SubmissionTokenType.SubmissionToken
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TokenType)
            .WithErrorMessage("Token must be provided when Token Type is specified.");
    }

    [Fact]
    public void Validate_MinimalValidRequest_PassesValidation()
    {
        // Arrange
        var request = new GetFormPublicAccessRequest { FormId = 1 };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}

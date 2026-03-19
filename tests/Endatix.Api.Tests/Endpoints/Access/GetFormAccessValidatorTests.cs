using Endatix.Api.Endpoints.Access;
using Endatix.Core.Authorization.Access;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Access;

public class GetFormAccessValidatorTests
{
    private readonly GetFormPublicAccessValidator _validator;

    public GetFormAccessValidatorTests()
    {
        _validator = new GetFormPublicAccessValidator();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(999999)]
    public void Validate_ValidFormId_PassesValidation(long formId)
    {
        var request = new GetFormPublicAccessRequest { FormId = formId };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.FormId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_InvalidFormId_ReturnsError(long formId)
    {
        var request = new GetFormPublicAccessRequest { FormId = formId };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.FormId)
            .WithErrorMessage("'Form Id' must be greater than '0'.");
    }

    [Fact]
    public void Validate_MinimalValidRequest_PassesValidation()
    {
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_TokenWithTokenType_PassesValidation()
    {
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1,
            Token = "12345.1234567890.r.signature",
            TokenType = SubmissionTokenType.AccessToken
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Token);
        result.ShouldNotHaveValidationErrorFor(x => x.TokenType);
    }

    [Fact]
    public void Validate_TokenWithSubmissionTokenType_PassesValidation()
    {
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1,
            Token = "12345.1234567890.r.signature",
            TokenType = SubmissionTokenType.SubmissionToken
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Token);
        result.ShouldNotHaveValidationErrorFor(x => x.TokenType);
    }

    [Fact]
    public void Validate_TokenWithoutTokenType_ReturnsError()
    {
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1,
            Token = "valid.token",
            TokenType = null
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TokenType)
            .WithErrorMessage("'Token Type' must not be empty.");
    }

    [Theory]
    [InlineData(SubmissionTokenType.AccessToken)]
    [InlineData(SubmissionTokenType.SubmissionToken)]
    public void Validate_ValidTokenType_PassesValidation(SubmissionTokenType tokenType)
    {
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1,
            Token = "valid.token",
            TokenType = tokenType
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.TokenType);
    }

    [Fact]
    public void Validate_EmptyTokenWithNullTokenType_PassesValidation()
    {
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1,
            Token = null,
            TokenType = null
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_OnlyFormId_PassesValidation()
    {
        var request = new GetFormPublicAccessRequest
        {
            FormId = 100
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_PublicFormRequest_PassesValidation()
    {
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1,
            Token = null,
            TokenType = null
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_AccessTokenRequest_PassesValidation()
    {
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1,
            Token = "12345.1234567890.r.signature",
            TokenType = SubmissionTokenType.AccessToken
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_SubmissionTokenRequest_PassesValidation()
    {
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1,
            Token = "long-lived-submission-token",
            TokenType = SubmissionTokenType.SubmissionToken
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_InvalidTokenTypeValue_ReturnsError()
    {
        var request = new GetFormPublicAccessRequest
        {
            FormId = 1,
            Token = "valid.token",
            TokenType = null
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TokenType);
    }
}


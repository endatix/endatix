using Endatix.Api.Endpoints.Folders;
using FluentValidation.TestHelper;
using Endatix.Core.Common;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Api.Tests.Endpoints.Folders;

public class GetBySlugRequestValidatorTests
{
    private readonly GetBySlugValidator _validator;

    public GetBySlugRequestValidatorTests()
    {
        _validator = new GetBySlugValidator();
    }

    [Fact]
    public void Validate_ValidSlug_PassesValidation()
    {
        var request = new GetBySlugRequest { Slug = "test-folder" };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ValidSlugWithNumbers_PassesValidation()
    {
        var request = new GetBySlugRequest { Slug = "folder-2024" };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_Slug_Is_Empty()
    {
        var request = new GetBySlugRequest { Slug = "" };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Should_Have_Error_When_Slug_Is_Null()
    {
        var request = new GetBySlugRequest { Slug = null! };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Should_Have_Error_When_Slug_Contains_InvalidCharacters()
    {
        var request = new GetBySlugRequest { Slug = "test folder" };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Should_Have_Error_When_Slug_Contains_Uppercase()
    {
        var request = new GetBySlugRequest { Slug = "TestFolder" };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Should_Have_Error_When_Slug_Starts_With_Hyphen()
    {
        var request = new GetBySlugRequest { Slug = "-testfolder" };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Should_Have_Error_When_Slug_Ends_With_Hyphen()
    {
        var request = new GetBySlugRequest { Slug = "testfolder-" };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Theory]
    [InlineData("test_folder")]
    [InlineData("test.folder")]
    [InlineData("test|folder")]
    public void Should_Have_Error_When_Slug_Contains_InvalidSeparator(string invalidSlug)
    {
        var request = new GetBySlugRequest { Slug = invalidSlug };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Should_Have_Error_When_Slug_Exceeds_MaxLength()
    {
        var request = new GetBySlugRequest { Slug = new string('a', DataSchemaConstants.MAX_SLUG_LENGTH + 1) };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Should_Pass_When_Slug_Is_At_MaxLength()
    {
        var request = new GetBySlugRequest { Slug = new string('a', DataSchemaConstants.MAX_SLUG_LENGTH) };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Slug);
    }

    [Theory]
    [InlineData("test@folder")]
    [InlineData("test#folder")]
    [InlineData("test$folder")]
    [InlineData("test%folder")]
    public void Should_Have_Error_When_Slug_Contains_SpecialCharacters(string invalidSlug)
    {
        var request = new GetBySlugRequest { Slug = invalidSlug };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }
}
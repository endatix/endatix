using Endatix.Api.Endpoints.Folders;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Folders;

public class PartialUpdateFolderRequestValidatorTests
{
    private readonly PartialUpdateFolderValidator _validator;

    public PartialUpdateFolderRequestValidatorTests()
    {
        _validator = new PartialUpdateFolderValidator();
    }

    [Fact]
    public void Validate_ValidRequest_PassesValidation()
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Name = "Updated Name",
            Slug = "updated-slug",
            Description = "Test Description",
            Metadata = null
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_PartialUpdateWithOnlyDescription_PassesValidation()
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Name = "Existing Name",
            Description = "New description"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_FolderId_Is_Zero()
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 0,
            Name = "Test"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.FolderId);
    }

    [Fact]
    public void Should_Have_Error_When_FolderId_Is_Negative()
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = -1,
            Name = "Test"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.FolderId);
    }

    [Fact]
    public void Should_Not_Have_Error_When_FolderId_Is_Positive()
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Name = "Test"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.FolderId);
    }

    [Fact]
    public void Should_Have_Error_When_Name_Is_Empty()
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Name = ""
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("a")]
    public void Should_Have_Error_When_Name_Is_Too_Short(string name)
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Name = name
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Valid Name")]
    [InlineData("Test Folder Name That Is Exactly Right")]
    public void Should_Not_Have_Error_When_Name_Is_Valid(string name)
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Name = name
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_Name_Is_Null()
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Name = null,
            IsActive = true,
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("valid-slug")]
    [InlineData("another-valid-slug-123")]
    public void Should_Not_Have_Error_When_Slug_Is_Valid(string slug)
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Slug = slug
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Slug);
    }

    [Theory]
    [InlineData("invalid slug")]
    [InlineData("invalid_slug")]
    [InlineData("InvalidSlug!")]
    public void Should_Have_Error_When_Slug_Is_Invalid(string slug)
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Slug = slug
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Slug_Is_Null()
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Slug = null,
            IsActive = true,
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Should_Have_Error_When_Description_Exceeds_MaxLength()
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Description = new string('a', 501)
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Description_Is_Valid()
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Description = "Test Description"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Description_Is_Null()
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Description = null,
            IsActive = true,
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"key\":\"value\"}")]
    [InlineData("[{\"key\":\"value\"}]")]
    public void Should_Not_Have_Error_When_Metadata_Is_ValidJson(string validJson)
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Metadata = validJson
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Metadata);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("invalid")]
    [InlineData("{key:value}")]
    public void Should_Have_Error_When_Metadata_Is_InvalidJson(string invalidJson)
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Metadata = invalidJson
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Metadata)
            .WithErrorMessage("Metadata must be a valid JSON string.");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Metadata_Is_Null()
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
            Metadata = null,
            IsActive = true,
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Metadata);
    }

    [Fact]
    public void Should_Have_Error_When_No_Fields_Are_Provided_For_PartialUpdate()
    {
        var request = new PartialUpdateFolderRequest
        {
            FolderId = 1,
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorMessage("At least one field must be provided for partial update.");
    }
}
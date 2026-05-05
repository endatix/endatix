using Endatix.Api.Endpoints.Folders;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.Folders;

public class CreateFolderRequestValidatorTests
{
    private readonly CreateFolderValidator _validator;

    public CreateFolderRequestValidatorTests()
    {
        _validator = new CreateFolderValidator();
    }

    [Fact]
    public void Validate_ValidRequest_PassesValidation()
    {
        var request = new CreateFolderRequest
        {
            Name = "Test Folder",
            Slug = "test-folder",
            Description = "Test Description",
            Metadata = null
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_Name_Is_Empty()
    {
        var request = new CreateFolderRequest
        {
            Name = "",
            Slug = "test-folder"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_Name_Is_Null()
    {
        var request = new CreateFolderRequest
        {
            Name = null,
            Slug = "test-folder"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("a")]
    public void Should_Have_Error_When_Name_Is_Too_Short(string name)
    {
        var request = new CreateFolderRequest
        {
            Name = name,
            Slug = "test-folder"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_Slug_Is_Empty()
    {
        var request = new CreateFolderRequest
        {
            Name = "Test Folder",
            Slug = ""
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Should_Have_Error_When_Slug_Is_Null()
    {
        var request = new CreateFolderRequest
        {
            Name = "Test Folder",
            Slug = null
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Should_Have_Error_When_Description_Exceeds_MaxLength()
    {
        var request = new CreateFolderRequest
        {
            Name = "Test Folder",
            Slug = "test-folder",
            Description = new string('a', 501)
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Description_Is_Valid()
    {
        var request = new CreateFolderRequest
        {
            Name = "Test Folder",
            Slug = "test-folder",
            Description = "Test Description"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Description_Is_Null()
    {
        var request = new CreateFolderRequest
        {
            Name = "Test Folder",
            Slug = "test-folder",
            Description = null
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"key\":\"value\"}")]
    [InlineData("[{\"key\":\"value\"}]")]
    [InlineData("{\"name\":\"test\",\"value\":123}")]
    public void Should_Not_Have_Error_When_Metadata_Is_ValidJson(string validJson)
    {
        var request = new CreateFolderRequest
        {
            Name = "Test Folder",
            Slug = "test-folder",
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
        var request = new CreateFolderRequest
        {
            Name = "Test Folder",
            Slug = "test-folder",
            Metadata = invalidJson
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Metadata)
            .WithErrorMessage("Metadata must be a valid JSON string.");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Metadata_Is_Null()
    {
        var request = new CreateFolderRequest
        {
            Name = "Test Folder",
            Slug = "test-folder",
            Metadata = null
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Metadata);
    }
}
using Endatix.Api.Endpoints.DataLists;
using Endatix.Infrastructure.Data.Config;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class PartialUpdateDataListValidatorTests
{
    private readonly PartialUpdateDataListValidator _sut = new();

    [Fact]
    public void Validate_ValidNameAndDescription_Passes()
    {
        // Arrange
        var request = new PartialUpdateDataListRequest
        {
            DataListId = 1,
            Name = "Cities",
            Description = "Major cities"
        };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_OmittedOptionalFields_Passes()
    {
        // Arrange
        var request = new PartialUpdateDataListRequest { DataListId = 1 };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyNameWhenProvided_Fails()
    {
        // Arrange
        var request = new PartialUpdateDataListRequest { DataListId = 1, Name = "   " };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_NameExceedsMaxLength_Fails()
    {
        // Arrange
        var request = new PartialUpdateDataListRequest
        {
            DataListId = 1,
            Name = new string('a', DataSchemaConstants.MAX_NAME_LENGTH + 1)
        };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_DescriptionExceedsMaxLength_Fails()
    {
        // Arrange
        var request = new PartialUpdateDataListRequest
        {
            DataListId = 1,
            Description = new string('a', DataSchemaConstants.MAX_DESCRIPTION_LENGTH + 1)
        };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_NonPositiveDataListId_Fails()
    {
        // Arrange
        var request = new PartialUpdateDataListRequest { DataListId = 0, Name = "Cities" };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DataListId);
    }
}

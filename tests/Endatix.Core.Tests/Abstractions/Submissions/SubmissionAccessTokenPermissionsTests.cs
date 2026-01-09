using Endatix.Core.Abstractions.Submissions;

namespace Endatix.Core.Tests.Abstractions.Submissions;

public class SubmissionAccessTokenPermissionsTests
{
    [Fact]
    public void View_ShouldHaveCorrectNameAndCode()
    {
        // Assert
        SubmissionAccessTokenPermissions.View.Name.Should().Be("view");
        SubmissionAccessTokenPermissions.View.Code.Should().Be('r');
    }

    [Fact]
    public void Edit_ShouldHaveCorrectNameAndCode()
    {
        // Assert
        SubmissionAccessTokenPermissions.Edit.Name.Should().Be("edit");
        SubmissionAccessTokenPermissions.Edit.Code.Should().Be('w');
    }

    [Fact]
    public void Export_ShouldHaveCorrectNameAndCode()
    {
        // Assert
        SubmissionAccessTokenPermissions.Export.Name.Should().Be("export");
        SubmissionAccessTokenPermissions.Export.Code.Should().Be('x');
    }

    [Fact]
    public void All_ShouldContainAllThreePermissions()
    {
        // Assert
        SubmissionAccessTokenPermissions.All.Should().HaveCount(3);
        SubmissionAccessTokenPermissions.All.Should().Contain(SubmissionAccessTokenPermissions.View);
        SubmissionAccessTokenPermissions.All.Should().Contain(SubmissionAccessTokenPermissions.Edit);
        SubmissionAccessTokenPermissions.All.Should().Contain(SubmissionAccessTokenPermissions.Export);
    }

    [Fact]
    public void AllNames_ShouldContainAllPermissionNames()
    {
        // Assert
        SubmissionAccessTokenPermissions.AllNames.Should().HaveCount(3);
        SubmissionAccessTokenPermissions.AllNames.Should().Contain("view");
        SubmissionAccessTokenPermissions.AllNames.Should().Contain("edit");
        SubmissionAccessTokenPermissions.AllNames.Should().Contain("export");
    }

    [Theory]
    [InlineData("view", true)]
    [InlineData("edit", true)]
    [InlineData("export", true)]
    [InlineData("View", true)]  // Case insensitive
    [InlineData("EDIT", true)]  // Case insensitive
    [InlineData("invalid", false)]
    [InlineData("delete", false)]
    [InlineData("", false)]
    public void IsValid_ShouldValidatePermissionNames(string permissionName, bool expectedResult)
    {
        // Act
        var result = SubmissionAccessTokenPermissions.IsValid(permissionName);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("view")]
    [InlineData("View")]
    [InlineData("VIEW")]
    public void GetByName_ValidName_ShouldReturnPermission(string name)
    {
        // Act
        var result = SubmissionAccessTokenPermissions.GetByName(name);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("view");
        result.Code.Should().Be('r');
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("delete")]
    [InlineData("")]
    public void GetByName_InvalidName_ShouldReturnNull(string name)
    {
        // Act
        var result = SubmissionAccessTokenPermissions.GetByName(name);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData('r', "view")]
    [InlineData('w', "edit")]
    [InlineData('x', "export")]
    public void GetByCode_ValidCode_ShouldReturnPermission(char code, string expectedName)
    {
        // Act
        var result = SubmissionAccessTokenPermissions.GetByCode(code);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(expectedName);
        result.Code.Should().Be(code);
    }

    [Theory]
    [InlineData('a')]
    [InlineData('z')]
    [InlineData('R')]  // Case sensitive
    public void GetByCode_InvalidCode_ShouldReturnNull(char code)
    {
        // Act
        var result = SubmissionAccessTokenPermissions.GetByCode(code);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromNames_ValidNames_ShouldReturnPermissions()
    {
        // Arrange
        var names = new[] { "view", "edit" };

        // Act
        var result = SubmissionAccessTokenPermissions.FromNames(names);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(SubmissionAccessTokenPermissions.View);
        result.Should().Contain(SubmissionAccessTokenPermissions.Edit);
    }

    [Fact]
    public void FromNames_MixedValidAndInvalidNames_ShouldFilterOutInvalid()
    {
        // Arrange
        var names = new[] { "view", "invalid", "edit", "delete" };

        // Act
        var result = SubmissionAccessTokenPermissions.FromNames(names);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(SubmissionAccessTokenPermissions.View);
        result.Should().Contain(SubmissionAccessTokenPermissions.Edit);
    }

    [Fact]
    public void FromNames_CaseInsensitive_ShouldReturnPermissions()
    {
        // Arrange
        var names = new[] { "View", "EDIT", "ExPoRt" };

        // Act
        var result = SubmissionAccessTokenPermissions.FromNames(names);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(SubmissionAccessTokenPermissions.View);
        result.Should().Contain(SubmissionAccessTokenPermissions.Edit);
        result.Should().Contain(SubmissionAccessTokenPermissions.Export);
    }

    [Fact]
    public void FromCodesString_ValidCodes_ShouldReturnPermissions()
    {
        // Arrange
        var codesString = "rw";

        // Act
        var result = SubmissionAccessTokenPermissions.FromCodesString(codesString);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(SubmissionAccessTokenPermissions.View);
        result.Should().Contain(SubmissionAccessTokenPermissions.Edit);
    }

    [Fact]
    public void FromCodesString_AllCodes_ShouldReturnAllPermissions()
    {
        // Arrange
        var codesString = "rwx";

        // Act
        var result = SubmissionAccessTokenPermissions.FromCodesString(codesString);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(SubmissionAccessTokenPermissions.View);
        result.Should().Contain(SubmissionAccessTokenPermissions.Edit);
        result.Should().Contain(SubmissionAccessTokenPermissions.Export);
    }

    [Fact]
    public void FromCodesString_MixedValidAndInvalidCodes_ShouldFilterOutInvalid()
    {
        // Arrange
        var codesString = "razw";

        // Act
        var result = SubmissionAccessTokenPermissions.FromCodesString(codesString);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(SubmissionAccessTokenPermissions.View);
        result.Should().Contain(SubmissionAccessTokenPermissions.Edit);
    }

    [Fact]
    public void FromCodesString_EmptyString_ShouldReturnEmpty()
    {
        // Arrange
        var codesString = "";

        // Act
        var result = SubmissionAccessTokenPermissions.FromCodesString(codesString);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ToCodesString_MultiplePermissions_ShouldReturnCodeString()
    {
        // Arrange
        var permissions = new[]
        {
            SubmissionAccessTokenPermissions.View,
            SubmissionAccessTokenPermissions.Edit
        };

        // Act
        var result = SubmissionAccessTokenPermissions.ToCodesString(permissions);

        // Assert
        result.Should().Be("rw");
    }

    [Fact]
    public void ToCodesString_AllPermissions_ShouldReturnAllCodes()
    {
        // Arrange
        var permissions = new[]
        {
            SubmissionAccessTokenPermissions.View,
            SubmissionAccessTokenPermissions.Edit,
            SubmissionAccessTokenPermissions.Export
        };

        // Act
        var result = SubmissionAccessTokenPermissions.ToCodesString(permissions);

        // Assert
        result.Should().Be("rwx");
    }

    [Fact]
    public void ToCodesString_SinglePermission_ShouldReturnSingleCode()
    {
        // Arrange
        var permissions = new[] { SubmissionAccessTokenPermissions.Export };

        // Act
        var result = SubmissionAccessTokenPermissions.ToCodesString(permissions);

        // Assert
        result.Should().Be("x");
    }

    [Fact]
    public void ToCodesString_EmptyList_ShouldReturnEmptyString()
    {
        // Arrange
        var permissions = Array.Empty<SubmissionAccessTokenPermissions.AccessTokenPermission>();

        // Act
        var result = SubmissionAccessTokenPermissions.ToCodesString(permissions);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void EncodeNames_ValidNames_ShouldReturnCodeString()
    {
        // Arrange
        var names = new[] { "view", "edit" };

        // Act
        var result = SubmissionAccessTokenPermissions.EncodeNames(names);

        // Assert
        result.Should().Be("rw");
    }

    [Fact]
    public void EncodeNames_AllPermissionNames_ShouldReturnAllCodes()
    {
        // Arrange
        var names = new[] { "view", "edit", "export" };

        // Act
        var result = SubmissionAccessTokenPermissions.EncodeNames(names);

        // Assert
        result.Should().Be("rwx");
    }

    [Fact]
    public void EncodeNames_MixedValidAndInvalidNames_ShouldEncodeOnlyValid()
    {
        // Arrange
        var names = new[] { "view", "invalid", "edit" };

        // Act
        var result = SubmissionAccessTokenPermissions.EncodeNames(names);

        // Assert
        result.Should().Be("rw");
    }

    [Fact]
    public void EncodeNames_CaseInsensitive_ShouldEncodeCorrectly()
    {
        // Arrange
        var names = new[] { "View", "EDIT", "ExPoRt" };

        // Act
        var result = SubmissionAccessTokenPermissions.EncodeNames(names);

        // Assert
        result.Should().Be("rwx");
    }

    [Fact]
    public void DecodeToNames_ValidCodeString_ShouldReturnPermissionNames()
    {
        // Arrange
        var codesString = "rw";

        // Act
        var result = SubmissionAccessTokenPermissions.DecodeToNames(codesString);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("view");
        result.Should().Contain("edit");
    }

    [Fact]
    public void DecodeToNames_AllCodes_ShouldReturnAllNames()
    {
        // Arrange
        var codesString = "rwx";

        // Act
        var result = SubmissionAccessTokenPermissions.DecodeToNames(codesString);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("view");
        result.Should().Contain("edit");
        result.Should().Contain("export");
    }

    [Fact]
    public void DecodeToNames_MixedValidAndInvalidCodes_ShouldDecodeOnlyValid()
    {
        // Arrange
        var codesString = "razw";

        // Act
        var result = SubmissionAccessTokenPermissions.DecodeToNames(codesString);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("view");
        result.Should().Contain("edit");
    }

    [Fact]
    public void DecodeToNames_EmptyString_ShouldReturnEmpty()
    {
        // Arrange
        var codesString = "";

        // Act
        var result = SubmissionAccessTokenPermissions.DecodeToNames(codesString);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void EncodeAndDecode_RoundTrip_ShouldPreservePermissions()
    {
        // Arrange
        var originalNames = new[] { "view", "edit", "export" };

        // Act
        var encoded = SubmissionAccessTokenPermissions.EncodeNames(originalNames);
        var decoded = SubmissionAccessTokenPermissions.DecodeToNames(encoded);

        // Assert
        decoded.Should().BeEquivalentTo(originalNames);
    }
}

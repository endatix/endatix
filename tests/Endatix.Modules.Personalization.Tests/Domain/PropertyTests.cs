using Endatix.Core.Exceptions;
using Endatix.Modules.Personalization.Contracts;
using Endatix.Modules.Personalization.Domain;

namespace Endatix.Modules.Personalization.Tests.Domain;

public class PropertyTests
{
    [Fact]
    public void Constructor_SlugifiesVariableName_FromName()
    {
        Property property = new(new PropertyCreateArgs(
            TenantId: 1,
            FormId: 10,
            Name: "First Name!",
            DataType: AudienceDataTypeCodes.Text,
            SortOrder: 0));

        property.VariableName.Should().Be("first_name");
        property.Name.Should().Be("First Name!");
    }

    [Fact]
    public void Rename_ChangesName_Only()
    {
        Property property = new(new PropertyCreateArgs(
            TenantId: 1,
            FormId: 10,
            Name: "Email",
            DataType: AudienceDataTypeCodes.Text,
            SortOrder: 0));

        string originalVariable = property.VariableName;

        property.Rename("Work Email");

        property.Name.Should().Be("Work Email");
        property.VariableName.Should().Be(originalVariable);
        property.VariableName.Should().Be("email");
    }

    [Fact]
    public void Slugify_RejectsNameWithoutAlphanumerics()
    {
        Action act = () => Property.Slugify("!!!");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("Hello World", "hello_world")]
    [InlineData("  Age  ", "age")]
    [InlineData("Q1__Score", "q1_score")]
    public void Slugify_NormalizesExpectedShapes(string name, string expected)
    {
        Property.Slugify(name).Should().Be(expected);
    }

    [Fact]
    public void VariableNameError_NameWithoutAsciiLetterOrDigit_ReturnsMessage()
    {
        // Act
        string? error = Property.VariableNameError("!!!");

        // Assert
        error.Should().Be("Name must contain a letter or a digit (a-z, 0-9).");
    }

    [Fact]
    public void NameError_TooLong_ReturnsMessage()
    {
        string name = new('a', 101);

        string? error = Property.NameError(name);

        error.Should().Contain("100");
    }

    [Fact]
    public void Rename_BlankName_ThrowsDomainValidationException()
    {
        // Arrange
        Property property = new(new PropertyCreateArgs(
            TenantId: 1,
            FormId: 10,
            Name: "Email",
            DataType: AudienceDataTypeCodes.Text,
            SortOrder: 0));

        // Act
        Action act = () => property.Rename("   ");

        // Assert
        act.Should().Throw<DomainValidationException>().WithMessage("Name is required.*");
    }
}

using Endatix.Core.Exceptions;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Tests.Domain;

public class PropertyTests
{
    [Fact]
    public void Constructor_NameWithPunctuation_SlugifiesVariableName()
    {
        // Act
        Property property = TextPropertyNamed("First Name!");

        // Assert
        property.VariableName.Should().Be("first_name");
        property.Name.Should().Be("First Name!");
    }

    [Fact]
    public void Rename_NewName_ChangesNameOnly()
    {
        // Arrange
        Property property = TextPropertyNamed("Email");
        string originalVariable = property.VariableName;

        // Act
        property.Rename("Work Email");

        // Assert
        property.Name.Should().Be("Work Email");
        property.VariableName.Should().Be(originalVariable);
        property.VariableName.Should().Be("email");
    }

    [Fact]
    public void Slugify_NameWithoutAlphanumerics_ThrowsArgumentException()
    {
        // Act
        Action act = () => Property.Slugify("!!!");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("Hello World", "hello_world")]
    [InlineData("  Age  ", "age")]
    [InlineData("Q1__Score", "q1_score")]
    public void Slugify_ValidName_ReturnsExpectedShape(string name, string expected)
    {
        // Act
        string slug = Property.Slugify(name);

        // Assert
        slug.Should().Be(expected);
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
        // Arrange
        string name = new('a', 101);

        // Act
        string? error = Property.NameError(name);

        // Assert
        error.Should().Contain("100");
    }

    [Fact]
    public void Rename_BlankName_ThrowsDomainValidationException()
    {
        // Arrange
        Property property = TextPropertyNamed("Email");

        // Act
        Action act = () => property.Rename("   ");

        // Assert
        act.Should().Throw<DomainValidationException>().WithMessage("Name is required.*");
    }

    [Fact]
    public void Constructor_ChoiceTypeWithoutChoices_ThrowsDomainValidationException()
    {
        // Act
        Action act = () => PropertyOf(AudienceDataTypeCodes.SingleChoice);

        // Assert
        act.Should().Throw<DomainValidationException>();
    }

    internal static Property PropertyOf(
        string dataType,
        string? choicesJson = null,
        bool allowsOther = false) =>
        new(new PropertyCreateArgs(
            TenantId: 1,
            FormId: 10,
            Name: "Plan",
            DataType: dataType,
            SortOrder: 0,
            ChoicesJson: choicesJson,
            AllowsOther: allowsOther));

    private static Property TextPropertyNamed(string name) =>
        new(new PropertyCreateArgs(
            TenantId: 1,
            FormId: 10,
            Name: name,
            DataType: AudienceDataTypeCodes.Text,
            SortOrder: 0));
}

public class PropertyValueTests
{
    [Fact]
    public void ValueError_TooLong_ReturnsMessage()
    {
        // Arrange
        string value = new('a', PropertyValue.VALUE_MAX_LENGTH + 1);

        // Act
        string? error = PropertyValue.ValueError(value);

        // Assert
        error.Should().Contain("4000");
    }

    [Fact]
    public void Constructor_EmptyValue_ThrowsDomainValidationException()
    {
        // Act
        Action act = () => new PropertyValue(new PropertyValueCreateArgs(1, 2, 3, ""));

        // Assert
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void SetValue_EmptyValue_ThrowsDomainValidationException()
    {
        // Arrange
        PropertyValue cell = new(new PropertyValueCreateArgs(1, 2, 3, "Sofia"));

        // Act
        Action act = () => cell.SetValue("");

        // Assert
        act.Should().Throw<DomainValidationException>();
    }
}

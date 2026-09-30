using Endatix.Modules.Personalization.Contracts;
using Endatix.Modules.Personalization.Domain;

namespace Endatix.Modules.Personalization.Tests.Domain;

public class AudiencePropertyTests
{
    [Fact]
    public void Constructor_SlugifiesVariableName_FromName()
    {
        AudienceProperty property = new(new AudiencePropertyCreateArgs(
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
        AudienceProperty property = new(new AudiencePropertyCreateArgs(
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
        Action act = () => AudienceProperty.Slugify("!!!");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("Hello World", "hello_world")]
    [InlineData("  Age  ", "age")]
    [InlineData("Q1__Score", "q1_score")]
    public void Slugify_NormalizesExpectedShapes(string name, string expected)
    {
        AudienceProperty.Slugify(name).Should().Be(expected);
    }
}

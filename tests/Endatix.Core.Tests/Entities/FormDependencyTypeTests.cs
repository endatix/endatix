using Endatix.Core.Entities;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

public class FormDependencyTypeTests
{
    [Fact]
    public void FromCode_WithValidCode_ReturnsDataListType()
    {
        // Act
        var dependencyType = FormDependencyType.FromCode("datalist");

        // Assert
        dependencyType.Should().Be(FormDependencyType.DataList);
        dependencyType.Code.Should().Be("datalist");
        dependencyType.Name.Should().Be("Data List");
    }

    [Fact]
    public void FromCode_WithInvalidCode_ThrowsArgumentException()
    {
        // Act
        Action act = () => FormDependencyType.FromCode("invalid");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("DATALIST")]
    [InlineData("DataList")]
    [InlineData("DaTaLiSt")]
    public void FromCode_WithUppercaseOrMixedCase_ReturnsDataListType(string code)
    {
        var dependencyType = FormDependencyType.FromCode(code);

        dependencyType.Should().Be(FormDependencyType.DataList);
        dependencyType.Code.Should().Be("datalist");
    }

    [Fact]
    public void FromCode_WithNullOrWhitespace_Throws()
    {
        Action nullAct = () => FormDependencyType.FromCode(null!);
        nullAct.Should().Throw<ArgumentNullException>();

        Action emptyAct = () => FormDependencyType.FromCode(string.Empty);
        emptyAct.Should().Throw<ArgumentException>();

        Action whitespaceAct = () => FormDependencyType.FromCode("   ");
        whitespaceAct.Should().Throw<ArgumentException>();
    }
}

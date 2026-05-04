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
}

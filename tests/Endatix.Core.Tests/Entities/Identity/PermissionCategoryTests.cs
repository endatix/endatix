using Endatix.Core.Entities.Identity;

namespace Endatix.Core.Tests.Entities.Identity;

public sealed class PermissionCategoryTests
{
    [Fact]
    public void FromCode_KnownCode_ReturnsCatalogCategory()
    {
        PermissionCategory.FromCode("FORMS").Should().Be(PermissionCategory.Forms);
        PermissionCategory.FromCode("platform").Name.Should().Be("Platform Management");
    }

    [Fact]
    public void FromCode_UnknownCode_Throws()
    {
        var act = () => PermissionCategory.FromCode("nope");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetAll_ReturnsEveryCategory()
    {
        PermissionCategory.GetAll().Should().HaveCount(10).And.Contain(PermissionCategory.Custom);
        PermissionCategory.Access.ToString().Should().Be("Access");
    }

    [Fact]
    public void RelationalOperators_OrderByCode()
    {
        // Arrange
        PermissionCategory? none = null;
        var sameAccess = PermissionCategory.FromCode("access");
        var sameForms = PermissionCategory.FromCode("forms");

        // Act & Assert
        (PermissionCategory.Access < PermissionCategory.Forms).Should().BeTrue();
        (PermissionCategory.Access <= sameAccess).Should().BeTrue();
        (PermissionCategory.Forms > PermissionCategory.Access).Should().BeTrue();
        (PermissionCategory.Forms >= sameForms).Should().BeTrue();
        (none < PermissionCategory.Access).Should().BeTrue();
        (PermissionCategory.Access > none).Should().BeTrue();
        PermissionCategory.Access.CompareTo(null).Should().Be(1);
    }
}

using Endatix.Core.Common;
using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class TenantTests
{
    [Fact]
    public void Constructor_ValidNameAndSlug_SetsProperties()
    {
        // Arrange & Act
        var tenant = new Tenant("Acme Surveys", "acme-surveys", "Demo tenant");

        // Assert
        tenant.Name.Should().Be("Acme Surveys");
        tenant.Slug.Should().Be("acme-surveys");
        tenant.Description.Should().Be("Demo tenant");
    }

    [Fact]
    public void Constructor_EmptyName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new Tenant("", "acme");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ReservedSlug_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new Tenant("Admin Org", "admin");

        // Assert
        act.Should().Throw<ArgumentException>();
        UrlSlugNormalizer.IsReserved("admin").Should().BeTrue();
    }

    [Fact]
    public void Constructor_InvalidSlugFormat_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new Tenant("Bad", "Bad_Slug");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateName_ValidName_UpdatesName()
    {
        // Arrange
        var tenant = new Tenant("Acme", "acme");

        // Act
        tenant.UpdateName("Acme Corp");

        // Assert
        tenant.Name.Should().Be("Acme Corp");
        tenant.Slug.Should().Be("acme");
    }

    [Fact]
    public void UpdateDescription_SetsDescription()
    {
        // Arrange
        var tenant = new Tenant("Acme", "acme");

        // Act
        tenant.UpdateDescription("Updated");

        // Assert
        tenant.Description.Should().Be("Updated");
    }
}

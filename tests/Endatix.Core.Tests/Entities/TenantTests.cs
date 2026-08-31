using Endatix.Core.Common;
using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class TenantTests
{
    private const string ValidSlug = "xk9mp2qr";

    [Fact]
    public void Constructor_ValidNameAndPublicId_SetsProperties()
    {
        var tenant = new Tenant("Acme Surveys", ValidSlug, "Demo tenant");

        tenant.Name.Should().Be("Acme Surveys");
        tenant.Slug.Should().Be(ValidSlug);
        tenant.Description.Should().Be("Demo tenant");
    }

    [Fact]
    public void Constructor_EmptyName_ThrowsArgumentException()
    {
        var act = () => new Tenant("", ValidSlug);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NameDerivedSlug_ThrowsArgumentException()
    {
        var act = () => new Tenant("Acme Regional Surveys", UrlSlugNormalizer.FromDisplayName("Acme Regional Surveys"));

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("acme")]           // too short
    [InlineData("XK9MP2QR")]       // uppercase is outside the alphabet
    [InlineData("xk9mp2q-")]       // hyphen is outside the alphabet
    public void Constructor_InvalidSlugFormat_ThrowsArgumentException(string slug)
    {
        var act = () => new Tenant("Bad", slug);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateName_ValidName_DoesNotChangeSlug()
    {
        var tenant = new Tenant("Acme", ValidSlug);

        tenant.UpdateName("Acme Corp");

        tenant.Name.Should().Be("Acme Corp");
        tenant.Slug.Should().Be(ValidSlug);
    }

    [Fact]
    public void UpdateDescription_SetsDescription()
    {
        var tenant = new Tenant("Acme", ValidSlug);

        tenant.UpdateDescription("Updated");

        tenant.Description.Should().Be("Updated");
    }
}

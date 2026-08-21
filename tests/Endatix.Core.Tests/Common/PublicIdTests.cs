using Endatix.Core.Common;

namespace Endatix.Core.Tests.Common;

public class PublicIdTests
{
    [Fact]
    public void IsValidTenantSlug_TwelveAlphabetChars_ReturnsTrue()
    {
        PublicId.IsValidTenantSlug("xK9mP2qR8vNw").Should().BeTrue();
        PublicId.IsValidTenantSlug("AZaz09_-AAAA").Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("thirteencharsX")]
    [InlineData("acme-regional-surveys")]
    [InlineData("Bad Slug!!!!")]
    [InlineData("xxxxxxxxxxxx!")]
    public void IsValidTenantSlug_Invalid_ReturnsFalse(string? value)
    {
        PublicId.IsValidTenantSlug(value).Should().BeFalse();
    }

    [Fact]
    public void IsValidTenantSlug_TypicalNameSlug_IsUsuallyWrongLength()
    {
        var fromName = UrlSlugNormalizer.FromDisplayName("Acme Regional Surveys");
        fromName.Should().Be("acme-regional-surveys");
        PublicId.IsValidTenantSlug(fromName).Should().BeFalse();
    }
}

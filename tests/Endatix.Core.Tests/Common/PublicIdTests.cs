using Endatix.Core.Common;

namespace Endatix.Core.Tests.Common;

public class PublicIdTests
{
    [Fact]
    public void IsValidTenantSlug_EightAlphabetChars_ReturnsTrue()
    {
        PublicId.IsValidTenantSlug("xK9mP2qR").Should().BeTrue();
        PublicId.IsValidTenantSlug("AZaz09_-").Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("xK9mP2qR8vNw")]
    [InlineData("thirteencharsX")]
    [InlineData("acme-regional-surveys")]
    [InlineData("Bad Slug!!!!")]
    [InlineData("xxxxxxx!")]
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

    [Theory]
    [InlineData("xK9mP2qR", true)]
    [InlineData("abcdefgh", true)]
    [InlineData("abc12345", false)]
    [InlineData("12345678", false)]
    [InlineData("ab12-_XY", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsLetterHeavy_ReturnsExpected(string? value, bool expected)
    {
        PublicId.IsLetterHeavy(value).Should().Be(expected);
    }
}

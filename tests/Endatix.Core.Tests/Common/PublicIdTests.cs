using Endatix.Core.Common;

namespace Endatix.Core.Tests.Common;

public class PublicIdTests
{
    [Fact]
    public void IsValidShortSlug_EightAlphabetChars_ReturnsTrue()
    {
        PublicId.IsValidShortSlug("xK9mP2qR").Should().BeTrue();
        PublicId.IsValidShortSlug("AZaz09xy").Should().BeTrue();
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
    [InlineData("AZaz09_-")]
    [InlineData("jj-8VjcR")]
    public void IsValidShortSlug_Invalid_ReturnsFalse(string? value)
    {
        PublicId.IsValidShortSlug(value).Should().BeFalse();
    }

    [Fact]
    public void IsValidShortSlug_TypicalNameSlug_IsUsuallyWrongLength()
    {
        var fromName = UrlSlugNormalizer.FromDisplayName("Acme Regional Surveys");
        fromName.Should().Be("acme-regional-surveys");
        PublicId.IsValidShortSlug(fromName).Should().BeFalse();
    }

    [Fact]
    public void ShortSlugLength_StaysEight_SoIssuedUrlsRemainStable()
    {
        // Changing this invalidates every slug already handed out in a URL.
        PublicId.ShortSlugLength.Should().Be(8);
    }

    [Theory]
    [InlineData("xK9mP2qR", true)]
    [InlineData("abcdefgh", true)]
    [InlineData("abc12345", false)]
    [InlineData("12345678", false)]
    [InlineData("ab12XYZZ", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsLetterHeavy_ReturnsExpected(string? value, bool expected)
    {
        PublicId.IsLetterHeavy(value).Should().Be(expected);
    }
}

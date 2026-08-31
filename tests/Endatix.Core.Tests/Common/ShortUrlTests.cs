using Endatix.Core.Common;

namespace Endatix.Core.Tests.Common;

public class ShortUrlTests
{
    [Fact]
    public void IsValid_EightAlphabetChars_ReturnsTrue()
    {
        ShortUrl.IsValid("xk9mp2qr").Should().BeTrue();
        ShortUrl.IsValid("az09wxyz").Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("xk9mp2qr8vnw")]
    [InlineData("thirteencharsX")]
    [InlineData("acme-regional-surveys")]
    [InlineData("bad slug!!!!")]
    [InlineData("xK9mP2qR")] // uppercase is outside the alphabet
    [InlineData("xxxxxxx!")]
    [InlineData("az09wx_-")]
    [InlineData("jj-8vjcr")]
    public void IsValid_Malformed_ReturnsFalse(string? value)
    {
        ShortUrl.IsValid(value).Should().BeFalse();
    }

    [Fact]
    public void IsValid_NameDerivedSlug_ReturnsFalse()
    {
        var fromName = UrlSlugNormalizer.FromDisplayName("Acme Regional Surveys");
        fromName.Should().Be("acme-regional-surveys");
        ShortUrl.IsValid(fromName).Should().BeFalse();
    }

    [Fact]
    public void StandardLength_StaysEight_SoIssuedUrlsRemainStable()
    {
        // Changing this invalidates every slug already handed out in a URL.
        ShortUrl.StandardLength.Should().Be(8);
    }

    [Theory]
    [InlineData("  XK9MP2QR  ", "xk9mp2qr")]
    [InlineData("xk9mp2qr", "xk9mp2qr")]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    public void Normalize_FoldsInboundUrlSegmentsToStoredForm(string? value, string? expected)
    {
        ShortUrl.Normalize(value).Should().Be(expected);
    }

    [Fact]
    public void Alphabet_IsLowercaseOnly_SoProviderCollationCannotDiverge()
    {
        // A mixed-case alphabet means PostgreSQL (case-sensitive) and SQL Server (default
        // case-insensitive collation) disagree on what IX_Tenants_ShortUrl considers a duplicate.
        ShortUrl.Alphabet.Should().Be(ShortUrl.Alphabet.ToLowerInvariant());
        ShortUrl.Alphabet.Should().HaveLength(36);
    }

    [Theory]
    [InlineData("xk9mp2qr", true)]
    [InlineData("abcdefgh", true)]
    [InlineData("abc12345", false)]
    [InlineData("12345678", false)]
    [InlineData("ab12wxyz", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsLetterHeavy_ReturnsExpected(string? value, bool expected)
    {
        ShortUrl.IsLetterHeavy(value).Should().Be(expected);
    }
}

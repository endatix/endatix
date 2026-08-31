using Endatix.Core.Common;

namespace Endatix.Core.Tests.Common;

public class PublicIdTests
{
    [Fact]
    public void IsValidShortSlug_EightAlphabetChars_ReturnsTrue()
    {
        PublicId.IsValidShortSlug("xk9mp2qr").Should().BeTrue();
        PublicId.IsValidShortSlug("az09wxyz").Should().BeTrue();
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
    [InlineData("  XK9MP2QR  ", "xk9mp2qr")]
    [InlineData("xk9mp2qr", "xk9mp2qr")]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    public void Normalize_FoldsInboundUrlSegmentsToStoredForm(string? value, string? expected)
    {
        PublicId.Normalize(value).Should().Be(expected);
    }

    [Fact]
    public void Alphabet_IsLowercaseOnly_SoProviderCollationCannotDiverge()
    {
        // A mixed-case alphabet means PostgreSQL (case-sensitive) and SQL Server (default
        // case-insensitive collation) disagree on what IX_Tenants_Slug considers a duplicate.
        PublicId.Alphabet.Should().Be(PublicId.Alphabet.ToLowerInvariant());
        PublicId.Alphabet.Should().HaveLength(36);
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
        PublicId.IsLetterHeavy(value).Should().Be(expected);
    }
}

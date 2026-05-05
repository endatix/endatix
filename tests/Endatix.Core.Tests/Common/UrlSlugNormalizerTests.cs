using Endatix.Core.Common;

namespace Endatix.Core.Tests.Common;

public class UrlSlugNormalizerTests
{
    public UrlSlugNormalizerTests()
    {
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("Hello World", "hello-world")]
    [InlineData("  Mixed_case.Name ", "mixed-case-name")]
    [InlineData("a--b---c", "a-b-c")]
    [InlineData("-trim-", "trim")]
    [InlineData("123abc", "123abc")]
    public async Task Normalize_ValidInput_ReturnsExpectedSlug(string raw, string expected)
    {
        // Arrange & Act
        var result = await Task.Run(() => UrlSlugNormalizer.Normalize(raw));

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task FromDisplayName_ValidInput_ReturnsNormalizedSlug()
    {
        // Arrange
        const string name = "Acme Regional Surveys";

        // Act
        var result = await Task.Run(() => UrlSlugNormalizer.FromDisplayName(name));

        // Assert
        result.Should().Be(UrlSlugNormalizer.Normalize(name));
    }

    [Fact]
    public async Task Normalize_NonAsciiLetters_RemovesNonAsciiCharacters()
    {
        // Arrange & Act
        var result = await Task.Run(() => UrlSlugNormalizer.Normalize("café-door"));

        // Assert
        result.Should().Be("caf-door");
    }

    [Fact]
    public async Task Normalize_LongInput_RespectsMaxLengthAndTrimsTrailingHyphen()
    {
        // Arrange
        var longBase = new string('a', UrlSlugNormalizer.MAX_SLUG_LENGTH + 20);

        // Act
        var result = await Task.Run(() => UrlSlugNormalizer.Normalize(longBase));

        // Assert
        result.Length.Should().BeLessThanOrEqualTo(UrlSlugNormalizer.MAX_SLUG_LENGTH);
        result.Should().NotStartWith("-");
        result.Should().NotEndWith("-");
    }

    [Theory]
    [InlineData("ok-slug", true)]
    [InlineData("a", true)]
    [InlineData("a1", true)]
    [InlineData("", false)]
    [InlineData("-bad", false)]
    [InlineData("bad-", false)]
    [InlineData("Bad", false)]
    [InlineData("bad_slug", false)]
    [InlineData("bad space", false)]
    public async Task IsValidFormat_ValidSlug_ReturnsExpected(string slug, bool expected)
    {
        // Arrange & Act
        var result = await Task.Run(() => UrlSlugNormalizer.IsValidFormat(slug));

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task IsValidFormat_OverlongSlug_ReturnsFalse()
    {
        // Arrange
        var slug = new string('a', UrlSlugNormalizer.MAX_SLUG_LENGTH + 1);

        // Act
        var result = await Task.Run(() => UrlSlugNormalizer.IsValidFormat(slug));

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("templates", true)]
    [InlineData("Templates", true)]
    [InlineData("share", true)]
    [InlineData("custom-name", false)]
    public async Task IsReserved_ReservedSlug_ReturnsExpected(string slug, bool expected)
    {
        // Arrange & Act
        var result = await Task.Run(() => UrlSlugNormalizer.IsReserved(slug));

        // Assert
        result.Should().Be(expected);
    }
}
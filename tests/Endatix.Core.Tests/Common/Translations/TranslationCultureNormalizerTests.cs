using Endatix.Core.Common.Translations;

namespace Endatix.Core.Tests.Common.Translations;

public class TranslationCultureNormalizerTests
{
    [Theory]
    [InlineData("ES", "es")]
    [InlineData(" en-US ", "en-us")]
    [InlineData("fr", "fr")]
    public void Normalize_TrimsAndLowercases(string input, string expected)
    {
        TranslationCultureNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("DEFAULT")]
    [InlineData(" Default ")]
    public void Normalize_PreservesSyntheticDefaultKey(string input)
    {
        TranslationCultureNormalizer.Normalize(input).Should().Be(SurveyJsTranslationKeys.DefaultKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_Whitespace_Throws(string? input)
    {
        Action act = () => TranslationCultureNormalizer.Normalize(input!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("not a culture!")]
    [InlineData("en_US")]
    [InlineData("123")]
    [InlineData("e")]
    [InlineData("-en")]
    public void Normalize_InvalidCultureCode_Throws(string input)
    {
        Action act = () => TranslationCultureNormalizer.Normalize(input);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("cultureCode");
    }

    [Theory]
    [InlineData("default", true)]
    [InlineData("DEFAULT", true)]
    [InlineData("es", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSyntheticDefaultKey_DetectsDefault(string? input, bool expected)
    {
        TranslationCultureNormalizer.IsSyntheticDefaultKey(input!).Should().Be(expected);
    }
}

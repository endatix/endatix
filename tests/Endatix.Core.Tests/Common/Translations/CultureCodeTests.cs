using Endatix.Core.Common.Translations;

namespace Endatix.Core.Tests.Common.Translations;

public class CultureCodeTests
{
    [Theory]
    [InlineData("ES", "es")]
    [InlineData(" en-US ", "en-us")]
    [InlineData("fr", "fr")]
    public void Parse_TrimsAndLowercases(string input, string expected)
    {
        CultureCode.Parse(input).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("DEFAULT")]
    [InlineData(" Default ")]
    public void Parse_PreservesSyntheticDefaultKey(string input)
    {
        CultureCode code = CultureCode.Parse(input);

        code.Value.Should().Be(SurveyJsTranslationKeys.DefaultKey);
        code.IsSyntheticDefault.Should().BeTrue();
        code.Should().Be(CultureCode.SyntheticDefault);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Whitespace_Throws(string? input)
    {
        Action act = () => CultureCode.Parse(input!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("not a culture!")]
    [InlineData("en_US")]
    [InlineData("123")]
    [InlineData("e")]
    [InlineData("-en")]
    public void Parse_InvalidCultureCode_Throws(string input)
    {
        Action act = () => CultureCode.Parse(input);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("cultureCode");
    }

    [Fact]
    public void Parse_TooLong_Throws()
    {
        string input = new('a', IHasTranslations.MAX_CULTURE_CODE_LENGTH + 1);

        Action act = () => CultureCode.Parse(input);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("default", true)]
    [InlineData("DEFAULT", true)]
    [InlineData("es", false)]
    public void IsSyntheticDefault_DetectsDefault(string input, bool expected)
    {
        CultureCode.Parse(input).IsSyntheticDefault.Should().Be(expected);
    }

    [Theory]
    [InlineData("es", true)]
    [InlineData("en-US", true)]
    [InlineData("default", true)]
    [InlineData("DEFAULT", true)]
    [InlineData("not a culture!", false)]
    [InlineData("en_US", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TryParse_ValidatesShape(string? input, bool expected)
    {
        CultureCode.TryParse(input, out _).Should().Be(expected);
    }

    [Theory]
    [InlineData("ES", "es")]
    [InlineData(" en-US ", "en-us")]
    [InlineData("Default", "default")]
    public void TryParse_Valid_ReturnsNormalized(string input, string expected)
    {
        CultureCode.TryParse(input, out CultureCode code).Should().BeTrue();
        code.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a culture!")]
    [InlineData("en_US")]
    public void TryParse_Invalid_ReturnsFalse(string? input)
    {
        CultureCode.TryParse(input, out CultureCode code).Should().BeFalse();
        code.Value.Should().BeNull();
    }
}

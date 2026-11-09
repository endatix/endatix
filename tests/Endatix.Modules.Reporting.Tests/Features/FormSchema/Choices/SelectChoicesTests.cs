using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Choices;

/// <summary>
/// Reading one stored answer in every shape SurveyJS writes: scalar, array, 3.x <c>{value, comment}</c> objects,
/// typed Other text in the value (<c>storeOthersAsComment: false</c>) and <c>{q}-Comment</c>.
/// </summary>
public sealed class SelectChoicesTests
{
    private const string SINGLE_COMMENT = """
        {"choiceCatalogs":{"q":{"multiple":false,"otherStorage":"comment","choices":[
          {"value":"a","id":1},{"value":"b","id":2},{"value":"other","id":3,"kind":"other"},
          {"value":"none","id":4,"kind":"none","exclusive":true},{"value":"refused","id":5,"kind":"refused","exclusive":true}]}}}
        """;

    private const string SINGLE_VALUE = """
        {"choiceCatalogs":{"q":{"multiple":false,"otherStorage":"value","choices":[
          {"value":"a","id":1},{"value":"b","id":2},{"value":"other","id":3,"kind":"other"},
          {"value":"none","id":4,"kind":"none","exclusive":true}]}}}
        """;

    private const string MULTIPLE_VALUE = """
        {"choiceCatalogs":{"q":{"multiple":true,"otherStorage":"value","choices":[
          {"value":"1","id":1},{"value":"2","id":2},{"value":"other","id":3,"kind":"other"},
          {"value":"none","id":4,"kind":"none","exclusive":true}]}}}
        """;

    private const string LEGACY_CATALOG = """
        {"choiceCatalogs":{"q":{"choices":[{"value":"a","id":1},{"value":"other","id":2}]}}}
        """;

    [Theory]
    [InlineData(SINGLE_COMMENT, """{"q":"a"}""", "a", null, "\"a\"")]
    [InlineData(SINGLE_COMMENT, """{"q":"other","q-Comment":"x"}""", "other", "\"x\"", "\"other\"")]
    [InlineData(SINGLE_COMMENT, """{"q":{"value":"other","comment":"x"}}""", "other", "\"x\"", "\"other\"")]
    [InlineData(SINGLE_COMMENT, """{"q":{"value":"a","comment":"why"}}""", "a", null, "\"a\"")]
    [InlineData(SINGLE_COMMENT, """{"q":"none"}""", "none", null, "\"none\"")]
    [InlineData(SINGLE_COMMENT, """{"q":"refused"}""", "refused", null, "\"refused\"")]
    [InlineData(SINGLE_VALUE, """{"q":"x"}""", "other", "\"x\"", "\"other\"")]
    [InlineData(SINGLE_VALUE, """{"q":"b"}""", "b", null, "\"b\"")]
    [InlineData(SINGLE_VALUE, """{"q":"none"}""", "none", null, "\"none\"")]
    [InlineData(SINGLE_VALUE, """{"q":"other","q-Comment":"typed before the flip"}""", "other", "\"typed before the flip\"", "\"other\"")]
    public void Read_SingleSelectShapes_GiveTheStoredValueAndOtherText(
        string codebook,
        string context,
        string expectedValue,
        string? expectedOtherText,
        string expectedCell)
    {
        var answer = ReadAnswer(codebook, context);

        answer.Values.Should().Equal(expectedValue);
        answer.OtherText?.GetRawText().Should().Be(expectedOtherText);
        if (expectedOtherText is null)
        {
            answer.OtherText.Should().BeNull();
        }

        answer.SingleValueJson()!.Value.GetRawText().Should().Be(expectedCell);
    }

    [Fact]
    public void Read_ValueStorage_IgnoresTheQuestionCommentWhenOtherIsNotChosen()
    {
        var answer = ReadAnswer(SINGLE_VALUE, """{"q":"a","q-Comment":"question comment"}""");

        answer.OtherText.Should().BeNull();
    }

    [Fact]
    public void Read_CheckboxObjectArray_ReadsValuesAndThePerItemOtherComment()
    {
        var answer = ReadAnswer(
            SINGLE_COMMENT.Replace("\"multiple\":false", "\"multiple\":true", StringComparison.Ordinal),
            """{"q":[{"value":"a"},{"value":"b","comment":"why b"},{"value":"other","comment":"x"}]}""");

        answer.Values.Should().Equal("a", "b", "other");
        answer.OtherText!.Value.GetString().Should().Be("x");
        answer.Has("b").Should().BeTrue();
        answer.Has("none").Should().BeFalse();
    }

    [Fact]
    public void Read_CheckboxValueStorage_TypedTextBecomesOther()
    {
        var answer = ReadAnswer(MULTIPLE_VALUE, """{"q":[1,"my own"]}""");

        answer.Values.Should().Equal("1", "other");
        answer.OtherText!.Value.GetString().Should().Be("my own");
        answer.Has("1").Should().BeTrue();
        answer.Has("other").Should().BeTrue();
    }

    [Fact]
    public void Read_CheckboxValueStorage_ObjectWithTypedTextBecomesOther()
    {
        var answer = ReadAnswer(MULTIPLE_VALUE, """{"q":[{"value":2},{"value":"my own"}]}""");

        answer.Values.Should().Equal("2", "other");
        answer.OtherText!.Value.GetString().Should().Be("my own");
    }

    [Fact]
    public void Read_ExclusiveItemWithComment_ReadsTheItem()
    {
        var answer = ReadAnswer(
            SINGLE_COMMENT.Replace("\"multiple\":false", "\"multiple\":true", StringComparison.Ordinal),
            """{"q":[{"value":"none","comment":"abc"}]}""");

        answer.Values.Should().Equal("none");
        answer.Has("none").Should().BeTrue();
        answer.OtherText.Should().BeNull();
    }

    [Fact]
    public void Read_MultipleSelect_SingleValueJsonKeepsTheRawArray()
    {
        var answer = ReadAnswer(MULTIPLE_VALUE, """{"q":[1,"my own"]}""");

        answer.SingleValueJson()!.Value.GetRawText().Should().Be("""[1,"my own"]""");
    }

    [Fact]
    public void Read_NumberValue_SingleValueJsonKeepsTheNumber()
    {
        var answer = ReadAnswer(SINGLE_COMMENT, """{"q":7}""");

        answer.SingleValueJson()!.Value.GetRawText().Should().Be("7");
        answer.Has("7").Should().BeTrue();
    }

    [Fact]
    public void Read_Missing_IsEmptyAndStillReadsTheCommentLikeBefore()
    {
        var answer = ReadAnswer(SINGLE_COMMENT, """{"q-Comment":"x"}""");

        answer.Values.Should().BeEmpty();
        answer.SingleValueJson().Should().BeNull();
        answer.OtherText!.Value.GetString().Should().Be("x");
    }

    [Fact]
    public void Read_BooleanAnswer_MatchesTrueAndFalseIgnoringCase()
    {
        var answer = ReadAnswer(SINGLE_COMMENT, """{"q":true}""");

        answer.Has("True").Should().BeTrue();
        answer.Has("false").Should().BeFalse();
    }

    [Fact]
    public void For_CodebookCompiledBeforeKinds_IsUnknownAndStillReadsObjectsAndComment()
    {
        var catalogs = ChoiceCatalogs.Read(LEGACY_CATALOG);

        catalogs.IsSelect("q").Should().BeFalse();
        using var context = JsonDocument.Parse("""{"q":[{"value":"other","comment":"x"}],"q-Comment":"y"}""");
        var answer = catalogs.For("q").Read(context.RootElement, "q");
        answer.Values.Should().Equal("other");
        answer.OtherText!.Value.GetString().Should().Be("x");
        catalogs.For("missing").Should().BeSameAs(SelectChoices.Unknown);
    }

    [Fact]
    public void Read_LegacyOtherObject_ReadsTheOtherProperty()
    {
        using var context = JsonDocument.Parse("""{"q":{"other":"legacy"}}""");

        var answer = SelectChoices.Unknown.Read(context.RootElement, "q");

        answer.OtherText!.Value.GetString().Should().Be("legacy");
        answer.Values.Should().BeEmpty();
    }

    [Fact]
    public void IsSelect_IsTrueForCatalogsWithMultiple()
    {
        var catalogs = ChoiceCatalogs.Read(SINGLE_COMMENT);

        catalogs.IsSelect("q").Should().BeTrue();
        catalogs.For("q").Choices.Select(choice => (choice.Value, choice.Id, choice.Kind, choice.Exclusive)).Should().Equal(
            ("a", 1, ChoiceKind.Choice, false),
            ("b", 2, ChoiceKind.Choice, false),
            ("other", 3, ChoiceKind.Other, false),
            ("none", 4, ChoiceKind.None, true),
            ("refused", 5, ChoiceKind.Refused, true));
    }

    [Fact]
    public void Read_EmptyOrMalformedCodebook_IsEmpty()
    {
        ChoiceCatalogs.Read("").IsSelect("q").Should().BeFalse();
        ChoiceCatalogs.Read("{}").For("q").Should().BeSameAs(SelectChoices.Unknown);
    }

    private static SelectAnswer ReadAnswer(string codebook, string context)
    {
        using var document = JsonDocument.Parse(context);
        return ChoiceCatalogs.Read(codebook).For("q").Read(document.RootElement.Clone(), "q");
    }
}

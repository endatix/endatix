using Endatix.Modules.Reporting.Tests.Features.Export.Tabular;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

/// <summary>Which files refuse a name two meanings would share, file by file.</summary>
public sealed class ExportNameRefusalTests
{
    // The checkbox's group keeps its native alias "Q5" in Shoji; question-index naming gives the fifth question "Q5".
    private const string GROUP_NAMED_LIKE_AN_INDEX = """
        {"pages":[{"elements":[{"type":"checkbox","name":"Q5","choices":["x","y"]},
        {"type":"text","name":"a"},{"type":"text","name":"b"},{"type":"text","name":"c"},
        {"type":"dropdown","name":"d","choices":["k"],"showNoneItem":true}]}]}
        """;

    [Theory]
    [InlineData("""{"aliasProfile":"crunch"}""", true)]
    [InlineData("""{"aliasProfile":"crunch","specialChoiceCodes":{"none":99}}""", false)]
    public async Task Shoji_GroupNamedLikeAQuestionIndex_IsRefusedOnlyWhenTheCodebookWritesIndexAliases(string settings, bool streams)
    {
        // Arrange
        var schema = PersonalizationPipeline.Compile(GROUP_NAMED_LIKE_AN_INDEX).Entity();

        // Act
        var (_, shoji) = await CrunchExport.PrepareBothAsync(new(schema, [], settings), TestContext.Current.CancellationToken);

        // Assert
        shoji.IsSuccess.Should().Be(streams);
    }

    [Theory]
    [InlineData("""{"aliasProfile":"crunch"}""")]
    [InlineData("""{"aliasProfile":"crunch","specialChoiceCodes":{"none":99}}""")]
    public async Task PlainCsv_GroupNamedLikeAQuestionIndex_Streams(string settings)
    {
        // Arrange
        var schema = PersonalizationPipeline.Compile(GROUP_NAMED_LIKE_AN_INDEX).Entity();

        // Act
        var csv = await SubmissionFileExport.PrepareAsync(new("csv", schema, [], settings), TestContext.Current.CancellationToken);

        // Assert
        csv.IsSuccess.Should().BeTrue();
    }
}

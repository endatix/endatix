using Endatix.Core.Abstractions.Exporting;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Microsoft.Extensions.Logging;
using NSubstitute;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

/// <summary>The warning an export logs when a single select's choices come from a source the codebook cannot read.</summary>
public sealed class DegradedQuestionWarningTests
{
    private const long FormId = 100;

    private readonly ILogger<ExportSchemas> _logger = Substitute.For<ILogger<ExportSchemas>>();
    private readonly List<(LogLevel Level, Dictionary<string, object?> Values)> _entries = [];

    // The logging generator reuses its state object, so the values are read while the call is made.
    public DegradedQuestionWarningTests()
    {
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _logger
            .WhenForAnyArgs(logger => logger.Log<Arg.AnyType>(default, default, default!, default, default!))
            .Do(call => _entries.Add((
                call.ArgAt<LogLevel>(0),
                call.ArgAt<IEnumerable<KeyValuePair<string, object?>>>(2).ToDictionary(StringComparer.Ordinal))));
    }

    [Fact]
    public async Task PrepareCodebookAsync_QuestionsCopyingFromADataList_LogsOneWarningNamingThem()
    {
        // Arrange
        var schema = Schema("""
            {"type":"dropdown","name":"brand","edxDataListId":"1234"},
            {"type":"radiogroup","name":"favourite","edxCarryForwardEnabled":true,"edxCarryForwardSources":["brand"]},
            {"type":"radiogroup","name":"kept","choices":["a"]}
            """);

        // Act
        await ExportSchemas().PrepareCodebookAsync(schema, new ExportOptions(), TestContext.Current.CancellationToken);

        // Assert
        var entry = _entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Values["FormId"].Should().Be(FormId);
        entry.Values["Questions"].Should().Be("favourite");
    }

    // csv-shoji reads the codebook without preparing it, and still warns.
    [Fact]
    public async Task CodebookAsync_QuestionCopyingFromAUrlList_LogsOneWarning()
    {
        // Arrange
        var schema = Schema("""
            {"type":"dropdown","name":"country","choicesByUrl":{"url":"https://example.com/countries"}},
            {"type":"radiogroup","name":"home","choicesFromQuestion":"country"}
            """);

        // Act
        await ExportSchemas().CodebookAsync(schema, TestContext.Current.CancellationToken);

        // Assert
        _entries.Should().ContainSingle().Which.Values["Questions"].Should().Be("home");
    }

    [Fact]
    public async Task PrepareCodebookAsync_EveryChoiceReadable_LogsNothing()
    {
        // Arrange
        var schema = Schema("""
            {"type":"checkbox","name":"Q1","choices":["1","2"]},
            {"type":"radiogroup","name":"Q2","choicesFromQuestion":"Q1"}
            """);

        // Act
        await ExportSchemas().PrepareCodebookAsync(schema, new ExportOptions(), TestContext.Current.CancellationToken);

        // Assert
        _entries.Should().BeEmpty();
    }

    private ExportSchemas ExportSchemas() =>
        new(Substitute.For<IFormSchemaRepository>(), TestExportSchemas.Catalog(), _logger);

    private static FormSchemaEntity Schema(string elements)
    {
        var compiled = new FormSchemaCompiler().CompilePersisted(
            $$"""{"pages":[{"name":"p","elements":[{{elements}}]}]}""");
        return new FormSchemaEntity(1, FormId, 1, compiled.FlatteningMapJson, compiled.CodebookJson);
    }
}

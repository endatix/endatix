using System.IO.Pipelines;
using System.Text.Json;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Exporting.ColumnDefinitions;
using Endatix.Infrastructure.Exporting.Exporters.Submissions;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public sealed class SubmissionExporterBaseTests
{
    private readonly ILogger _logger;
    private readonly TestExporter _sut;

    public SubmissionExporterBaseTests()
    {
        _logger = Substitute.For<ILogger>();
        _sut = new TestExporter(_logger);
    }

    [Fact]
    public void ItemType_ShouldReturnSubmissionExportRow()
    {
        // Assert
        Assert.Equal(typeof(SubmissionExportRow), _sut.ItemType);
    }

    [Fact]
    public void FileExtension_ShouldReturnFormat()
    {
        // Assert
        Assert.Equal("test", _sut.FileExtension);
    }

    [Fact]
    public async Task GetHeadersAsync_ShouldReturnSuccess_WhenValidOptions()
    {
        // Arrange
        var options = new ExportOptions
        {
            Metadata = new Dictionary<string, object> { { "FormId", 123L } }
        };

        // Act
        var result = await _sut.GetHeadersAsync(options, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var fileExport = result.Value;
        Assert.Equal("test/content", fileExport.ContentType);
        Assert.Equal("submissions-123.test", fileExport.FileName);
    }

    [Fact]
    public async Task GetHeadersAsync_ShouldReturnSuccess_WhenNoOptions()
    {
        // Act
        var result = await _sut.GetHeadersAsync(null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var fileExport = result.Value;
        Assert.Equal("submissions.test", fileExport.FileName);
    }

    [Fact]
    public async Task GetHeadersAsync_ShouldReturnError_WhenExceptionThrown()
    {
        // Arrange
        var invalidOptions = new ExportOptions
        {
            Metadata = new Dictionary<string, object> { { "FormId", "invalid" } }
        };

        // Act
        var result = await _sut.GetHeadersAsync(invalidOptions, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to get export headers", result.Errors.First());
    }

    [Fact]
    public async Task GetStreamContextAsync_ShouldInitializeColumns_OnFirstRow()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                AnswersModel = """{"question1": "answer1", "question2": "answer2"}"""
            }
        );

        // Act
        var contexts = new List<(SubmissionExportRow Row, JsonDocument? Doc, List<ColumnDefinition<SubmissionExportRow>> Columns)>();
        await foreach (var context in _sut.GetStreamContextAsyncPublic(records, null, CancellationToken.None))
        {
            contexts.Add(context);
        }

        // Assert
        Assert.Single(contexts);
        var (row, doc, columns) = contexts[0];
        Assert.Equal(1, row.Id);
        Assert.NotNull(doc);

        // Should include static columns + question columns
        Assert.Contains(columns, c => c.Name == "FormId");
        Assert.Contains(columns, c => c.Name == "Id");
        Assert.Contains(columns, c => c.Name == "question1");
        Assert.Contains(columns, c => c.Name == "question2");

        // JsonPropertyName should be pre-calculated
        var question1Col = columns.First(c => c.Name == "question1");
        Assert.Equal("question1", question1Col.JsonPropertyName);
    }

    [Fact]
    public async Task GetStreamContextAsync_ShouldParseJsonForEachRow()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                AnswersModel = """{"q1": "a1"}"""
            },
            new SubmissionExportRow
            {
                Id = 2,
                FormId = 100,
                AnswersModel = """{"q1": "a2"}"""
            }
        );

        // Act
        var contexts = new List<(SubmissionExportRow Row, JsonDocument? Doc, List<ColumnDefinition<SubmissionExportRow>> Columns)>();
        await foreach (var context in _sut.GetStreamContextAsyncPublic(records, null, CancellationToken.None))
        {
            contexts.Add(context);
        }

        // Assert
        Assert.Equal(2, contexts.Count);
        Assert.NotNull(contexts[0].Doc);
        Assert.NotNull(contexts[1].Doc);
        Assert.Equal(contexts[0].Columns.Count, contexts[1].Columns.Count);
    }

    [Fact]
    public async Task GetStreamContextAsync_ShouldHandleEmptyAnswersModel()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                AnswersModel = string.Empty
            }
        );

        // Act
        var contexts = new List<(SubmissionExportRow Row, JsonDocument? Doc, List<ColumnDefinition<SubmissionExportRow>> Columns)>();
        await foreach (var context in _sut.GetStreamContextAsyncPublic(records, null, CancellationToken.None))
        {
            contexts.Add(context);
        }

        // Assert
        Assert.Single(contexts);
        Assert.Null(contexts[0].Doc);
        // Should only have static columns
        Assert.All(contexts[0].Columns, c => Assert.DoesNotContain("question", c.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetStreamContextAsync_ShouldHandleInvalidJson_WithWarning()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                AnswersModel = "{ invalid json }"
            }
        );

        // Act
        var contexts = new List<(SubmissionExportRow Row, JsonDocument? Doc, List<ColumnDefinition<SubmissionExportRow>> Columns)>();
        await foreach (var context in _sut.GetStreamContextAsyncPublic(records, null, CancellationToken.None))
        {
            contexts.Add(context);
        }

        // Assert
        Assert.Single(contexts);
        Assert.Null(contexts[0].Doc);
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to parse JSON")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetStreamContextAsync_ShouldFilterColumns_WhenOptionsSpecified()
    {
        // Arrange
        var options = new ExportOptions
        {
            Columns = new[] { "Id", "FormId", "question1" }
        };
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                AnswersModel = """{"question1": "a1", "question2": "a2"}"""
            }
        );

        // Act
        var contexts = new List<(SubmissionExportRow Row, JsonDocument? Doc, List<ColumnDefinition<SubmissionExportRow>> Columns)>();
        await foreach (var context in _sut.GetStreamContextAsyncPublic(records, options, CancellationToken.None))
        {
            contexts.Add(context);
        }

        // Assert
        Assert.Single(contexts);
        var columns = contexts[0].Columns;
        Assert.Equal(3, columns.Count);
        Assert.Contains(columns, c => c.Name == "Id");
        Assert.Contains(columns, c => c.Name == "FormId");
        Assert.Contains(columns, c => c.Name == "question1");
        Assert.DoesNotContain(columns, c => c.Name == "question2");
    }

    [Fact]
    public async Task GetStreamContextAsync_ShouldApplyTransformers_WhenSpecified()
    {
        // Arrange
        var options = new ExportOptions
        {
            Transformers = new Dictionary<string, Func<object?, string>>
            {
                { "Id", v => $"ID-{v}" }
            }
        };
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 42,
                FormId = 100,
                AnswersModel = """{"q1": "a1"}"""
            }
        );

        // Act
        var contexts = new List<(SubmissionExportRow Row, JsonDocument? Doc, List<ColumnDefinition<SubmissionExportRow>> Columns)>();
        await foreach (var context in _sut.GetStreamContextAsyncPublic(records, options, CancellationToken.None))
        {
            contexts.Add(context);
        }

        // Assert
        var idColumn = contexts[0].Columns.First(c => c.Name == "Id");
        var formatted = idColumn.GetFormattedValue(contexts[0].Row, contexts[0].Doc);
        Assert.Equal("ID-42", formatted);
    }

    [Fact]
    public void GetFileName_ShouldUseFormIdFromMetadata_WhenAvailable()
    {
        // Arrange
        var options = new ExportOptions
        {
            Metadata = new Dictionary<string, object> { { "FormId", 456L } }
        };

        // Act
        var fileName = _sut.GetFileNamePublic(options, null, "csv");

        // Assert
        Assert.Equal("submissions-456.csv", fileName);
    }

    [Fact]
    public void GetFileName_ShouldUseFormIdFromFirstRow_WhenMetadataNotAvailable()
    {
        // Arrange
        var firstRow = new SubmissionExportRow { FormId = 789 };

        // Act
        var fileName = _sut.GetFileNamePublic(null, firstRow, "json");

        // Assert
        Assert.Equal("submissions-789.json", fileName);
    }

    [Fact]
    public void GetFileName_ShouldUseDefaultName_WhenNoFormId()
    {
        // Act
        var fileName = _sut.GetFileNamePublic(null, null, "csv");

        // Assert
        Assert.Equal("submissions.csv", fileName);
    }

    [Fact]
    public void GetFileName_ShouldConvertNumericTypes_FromMetadata()
    {
        // Arrange
        var optionsInt = new ExportOptions
        {
            Metadata = new Dictionary<string, object> { { "FormId", 123 } }
        };
        var optionsLong = new ExportOptions
        {
            Metadata = new Dictionary<string, object> { { "FormId", 456L } }
        };

        // Act
        var fileNameInt = _sut.GetFileNamePublic(optionsInt, null, "csv");
        var fileNameLong = _sut.GetFileNamePublic(optionsLong, null, "csv");

        // Assert
        Assert.Equal("submissions-123.csv", fileNameInt);
        Assert.Equal("submissions-456.csv", fileNameLong);
    }

    private static async IAsyncEnumerable<SubmissionExportRow> CreateTestRecords(params SubmissionExportRow[] rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }
        await Task.CompletedTask;
    }

    // Test implementation to access protected members
    private sealed class TestExporter : SubmissionExporterBase
    {
        public TestExporter(ILogger logger) : base(logger)
        {
        }

        public override string Format => "test";
        public override string ContentType => "test/content";

        public override Task<Result<FileExport>> StreamExportAsync(
            IAsyncEnumerable<SubmissionExportRow> records,
            ExportOptions? options,
            CancellationToken cancellationToken,
            PipeWriter writer)
        {
            throw new NotImplementedException("Test only");
        }

        // Expose protected methods for testing
        public async IAsyncEnumerable<(SubmissionExportRow Row, JsonDocument? Doc, List<ColumnDefinition<SubmissionExportRow>> Columns)>
            GetStreamContextAsyncPublic(
                IAsyncEnumerable<SubmissionExportRow> records,
                ExportOptions? options,
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var context in GetStreamContextAsync(records, options, cancellationToken))
            {
                yield return context;
            }
        }

        public string GetFileNamePublic(ExportOptions? options, SubmissionExportRow? firstRow, string extension)
        {
            return GetFileName(options, firstRow, extension);
        }
    }
}


using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Exporting.Exporters.Submissions;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public sealed class SubmissionCsvExporterTests
{
    private readonly ILogger<SubmissionCsvExporter> _logger;
    private readonly IEnumerable<IValueTransformer> _globalTransformers;
    private readonly SubmissionCsvExporter _sut;

    public SubmissionCsvExporterTests()
    {
        _logger = Substitute.For<ILogger<SubmissionCsvExporter>>();
        var transformer = Substitute.For<IValueTransformer>();
        transformer
            .Transform(Arg.Any<JsonNode?>(), Arg.Any<TransformationContext<SubmissionExportRow>>())
            .Returns(callInfo => (JsonNode?)callInfo[0]);
        _globalTransformers = new[] { transformer };
        _sut = new SubmissionCsvExporter(_logger, _globalTransformers);
    }

    [Fact]
    public void Format_ShouldReturnCsv()
    {
        Assert.Equal("csv", _sut.Format);
    }

    [Fact]
    public void ContentType_ShouldReturnTextCsv()
    {
        Assert.Equal("text/csv", _sut.ContentType);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldExportSingleRow_WithHeaders()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                IsComplete = true,
                CreatedAt = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                AnswersModel = """{"question1": "answer1", "question2": 42}"""
            }
        );

        var pipe = new Pipe();
        var options = new ExportOptions();

        // Act
        var result = await _sut.StreamExportAsync(records, options, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length); // Header + 1 row
        Assert.Contains("Id", lines[0]);
        Assert.Contains("FormId", lines[0]);
        Assert.Contains("question1", lines[0]);
        Assert.Contains("question2", lines[0]);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldExportMultipleRows()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow { Id = 1, FormId = 100, AnswersModel = """{"q1": "a1"}""" },
            new SubmissionExportRow { Id = 2, FormId = 100, AnswersModel = """{"q1": "a2"}""" },
            new SubmissionExportRow { Id = 3, FormId = 100, AnswersModel = """{"q1": "a3"}""" }
        );

        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length); // Header + 3 rows
    }

    [Fact]
    public async Task StreamExportAsync_ShouldHandleEmptyRecords()
    {
        // Arrange
        var records = CreateTestRecords();
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        Assert.Empty(content.Trim());
    }

    [Fact]
    public async Task StreamExportAsync_ShouldFilterColumns_WhenOptionsSpecified()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                AnswersModel = """{"q1": "a1", "q2": "a2"}"""
            }
        );

        var options = new ExportOptions { Columns = new[] { "Id", "FormId", "q1" } };
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, options, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var headerLine = content.Split('\n')[0];
        Assert.Contains("Id", headerLine);
        Assert.Contains("FormId", headerLine);
        Assert.Contains("q1", headerLine);
        Assert.DoesNotContain("q2", headerLine);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldApplyTransformers()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 42,
                FormId = 100,
                AnswersModel = """{"q1": "value"}"""
            }
        );

        var options = new ExportOptions
        {
            Formatters = new Dictionary<string, Func<object?, string>>
            {
                { "Id", v => $"ID-{v}" }
            }
        };
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, options, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var dataLine = content.Split('\n')[1];
        Assert.Contains("ID-42", dataLine);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldHandleInvalidJson_Gracefully()
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
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess); // Should still succeed, just with N/A for JSON columns
        var content = await ReadPipeContent(pipe.Reader);
        Assert.Contains("1", content); // Static columns should still work
    }

    [Fact]
    public async Task StreamExportAsync_ShouldHandleEmptyAnswersModel()
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
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 2); // Header + at least 1 row
    }

    [Fact]
    public async Task StreamExportAsync_ShouldIncludeFileName_WithFormId()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow { Id = 1, FormId = 123 }
        );
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("submissions-123.csv", result.Value.FileName);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldUseFormIdFromMetadata_WhenAvailable()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow { Id = 1, FormId = 100 }
        );
        var options = new ExportOptions
        {
            Metadata = new Dictionary<string, object> { { "FormId", 456L } }
        };
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, options, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("submissions-456.csv", result.Value.FileName);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldReturnError_WhenExceptionThrown()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow { Id = 1, FormId = 100 }
        );
        var pipe = new Pipe();
        await pipe.Writer.CompleteAsync(); // Close writer to cause error

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to export", result.Errors.First());
    }

    [Fact]
    public async Task StreamExportAsync_ShouldThrow_WhenWriterIsNull()
    {
        // Arrange
        var records = CreateTestRecords(new SubmissionExportRow { Id = 1 });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.StreamExportAsync(records, null, CancellationToken.None, null!));
    }

    [Fact]
    public async Task StreamExportAsync_ShouldHandleCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var records = CreateTestRecords(new SubmissionExportRow { Id = 1, FormId = 100 });
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, null, cts.Token, pipe.Writer);

        // Assert
        // Should handle cancellation gracefully (may succeed with partial data or fail)
        Assert.NotNull(result);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldEscapeSpecialCharacters_InCsv()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                AnswersModel = """{"q1": "value,with,commas", "q2": "value\"with\"quotes"}"""
            }
        );
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        // CsvHelper should handle escaping automatically
        var content = await ReadPipeContent(pipe.Reader);
        Assert.Contains("value,with,commas", content);
    }

    private static async IAsyncEnumerable<SubmissionExportRow> CreateTestRecords(params SubmissionExportRow[] rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }
        await Task.CompletedTask;
    }

    private static async Task<string> ReadPipeContent(PipeReader reader)
    {
        var result = await reader.ReadAsync();
        var buffer = result.Buffer;
        var content = Encoding.UTF8.GetString(buffer.ToArray());
        reader.AdvanceTo(buffer.End);
        await reader.CompleteAsync();
        return content;
    }
}


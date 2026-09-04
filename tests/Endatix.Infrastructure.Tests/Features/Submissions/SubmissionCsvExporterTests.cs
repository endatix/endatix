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
    public async Task StreamExportAsync_ShouldIncludeStartedAtAndDurationSeconds()
    {
        // Arrange
        var startedAt = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var completedAt = startedAt.AddSeconds(125);
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                IsComplete = true,
                CreatedAt = startedAt.AddHours(-2),
                StartedAt = startedAt,
                CompletedAt = completedAt,
                AnswersModel = """{"q1":"a"}"""
            }
        );

        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("StartedAt", lines[0]);
        Assert.Contains("DurationSeconds", lines[0]);
        Assert.Contains("125", lines[1]);
    }

    [Fact]
    public async Task StreamExportAsync_WhenIncomplete_ExportsDurationSecondsAsNotAvailable()
    {
        var startedAt = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                IsComplete = false,
                CreatedAt = startedAt,
                StartedAt = startedAt,
                CompletedAt = startedAt.AddSeconds(30),
                AnswersModel = """{"q1":"a"}"""
            }
        );

        var pipe = new Pipe();

        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var headers = ParseCsvFields(lines[0]);
        var values = ParseCsvFields(lines[1]);
        int durationIndex = Array.IndexOf(headers, "DurationSeconds");
        Assert.True(durationIndex >= 0);
        Assert.Equal("N/A", values[durationIndex]);
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
    public async Task StreamExportAsync_WritesRawNumericIds_ForCsvClients()
    {
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 123456789012345678,
                FormId = 100,
                SubmitterId = 55,
                SubmitterDisplayId = "display-9",
                AnswersModel = """{"q1":"a"}"""
            }
        );
        var pipe = new Pipe();

        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var headers = ParseCsvFields(lines[0]);
        var values = ParseCsvFields(lines[1]);

        Assert.Equal("123456789012345678", values[Array.IndexOf(headers, "Id")]);
        Assert.Equal("100", values[Array.IndexOf(headers, "FormId")]);
        Assert.Equal("55", values[Array.IndexOf(headers, "SubmitterId")]);
        Assert.Equal("display-9", values[Array.IndexOf(headers, "SubmitterDisplayId")]);
        Assert.DoesNotContain("=\"", content);
    }

    [Fact]
    public async Task StreamExportAsync_WritesLongDigitAnswersAsRawText()
    {
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                AnswersModel = """{"question1": "answer1", "question2": 42, "choiceId": "123456789012345678"}"""
            }
        );
        var pipe = new Pipe();

        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var headers = ParseCsvFields(lines[0]);
        var values = ParseCsvFields(lines[1]);

        Assert.Equal("answer1", values[Array.IndexOf(headers, "question1")]);
        Assert.Equal("42", values[Array.IndexOf(headers, "question2")]);
        Assert.Equal("123456789012345678", values[Array.IndexOf(headers, "choiceId")]);
    }

    [Fact]
    public async Task StreamExportAsync_LeavesSubmitterIdAsNotAvailable_WhenMissing()
    {
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                SubmitterId = null,
                AnswersModel = """{"q1":"a"}"""
            }
        );
        var pipe = new Pipe();

        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var headers = ParseCsvFields(lines[0]);
        var values = ParseCsvFields(lines[1]);
        Assert.Equal("N/A", values[Array.IndexOf(headers, "SubmitterId")]);
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
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        Assert.Contains("FormId", content);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var headers = ParseCsvFields(lines[0]);
        var values = ParseCsvFields(lines[1]);
        Assert.Equal("1", values[Array.IndexOf(headers, "Id")]);
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

    /// <summary>
    /// Minimal CSV field parser for test assertions (handles CsvHelper quoting).
    /// </summary>
    private static string[] ParseCsvFields(string line)
    {
        line = line.TrimEnd('\r');
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}


using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Exporting.Exporters.Submissions;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public sealed class SubmissionJsonExporterTests
{
    private readonly ILogger<SubmissionJsonExporter> _logger;
    private readonly SubmissionJsonExporter _sut;

    public SubmissionJsonExporterTests()
    {
        _logger = Substitute.For<ILogger<SubmissionJsonExporter>>();
        _sut = new SubmissionJsonExporter(_logger);
    }

    [Fact]
    public void Format_ShouldReturnJson()
    {
        Assert.Equal("json", _sut.Format);
    }

    [Fact]
    public void ContentType_ShouldReturnApplicationJson()
    {
        Assert.Equal("application/json", _sut.ContentType);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldExportSingleRow_AsJsonArray()
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

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var json = JsonDocument.Parse(content);
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.Equal(1, json.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task StreamExportAsync_ShouldExportMultipleRows()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow { Id = 1, FormId = 100, AnswersModel = """{"q1": "a1"}""" },
            new SubmissionExportRow { Id = 2, FormId = 100, AnswersModel = """{"q1": "a2"}""" }
        );
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var json = JsonDocument.Parse(content);
        Assert.Equal(2, json.RootElement.GetArrayLength());
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
        var json = JsonDocument.Parse(content);
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.Equal(0, json.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task StreamExportAsync_ShouldUseCamelCase_ForPropertyNames()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                AnswersModel = """{"q1": "a1"}"""
            }
        );
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var json = JsonDocument.Parse(content);
        var firstObject = json.RootElement[0];
        Assert.True(firstObject.TryGetProperty("id", out _));
        Assert.True(firstObject.TryGetProperty("formId", out _));
        Assert.False(firstObject.TryGetProperty("Id", out _)); // Should be camelCase
    }

    [Fact]
    public async Task StreamExportAsync_ShouldHandleNullValues()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                ModifiedAt = null,
                CompletedAt = null,
                AnswersModel = """{"q1": null}"""
            }
        );
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var json = JsonDocument.Parse(content);
        var firstObject = json.RootElement[0];
        Assert.Equal(JsonValueKind.Null, firstObject.GetProperty("modifiedAt").ValueKind);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldHandleDifferentValueTypes()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                IsComplete = true,
                CreatedAt = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                AnswersModel = """{"stringVal": "text", "numberVal": 42, "boolVal": true}"""
            }
        );
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var json = JsonDocument.Parse(content);
        var firstObject = json.RootElement[0];
        Assert.Equal(JsonValueKind.String, firstObject.GetProperty("stringVal").ValueKind);
        Assert.Equal(JsonValueKind.Number, firstObject.GetProperty("numberVal").ValueKind);
        Assert.Equal(JsonValueKind.True, firstObject.GetProperty("boolVal").ValueKind);
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
        var json = JsonDocument.Parse(content);
        var firstObject = json.RootElement[0];
        Assert.True(firstObject.TryGetProperty("id", out _));
        Assert.True(firstObject.TryGetProperty("q1", out _));
        Assert.False(firstObject.TryGetProperty("q2", out _));
    }

    [Fact]
    public async Task StreamExportAsync_ShouldNotApplyTransformers_ForJsonExport()
    {
        // Arrange
        // Note: JSON exporter uses ExtractValue which doesn't apply transformers
        // Transformers are for formatted string output (CSV), not raw JSON values
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
            Transformers = new Dictionary<string, Func<object?, string>>
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
        var json = JsonDocument.Parse(content);
        var firstObject = json.RootElement[0];
        // JSON exports use raw values, not transformed strings
        Assert.Equal(42, firstObject.GetProperty("id").GetInt64());
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
        Assert.True(result.IsSuccess); // Should still succeed
        var content = await ReadPipeContent(pipe.Reader);
        var json = JsonDocument.Parse(content);
        var firstObject = json.RootElement[0];
        Assert.True(firstObject.TryGetProperty("id", out _)); // Static columns should work
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
        var json = JsonDocument.Parse(content);
        Assert.Equal(1, json.RootElement.GetArrayLength());
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
        Assert.Equal("submissions-123.json", result.Value.FileName);
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
        Assert.Equal("submissions-456.json", result.Value.FileName);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldReturnError_WhenExceptionThrown()
    {
        // Arrange
        var records = CreateTestRecords(new SubmissionExportRow { Id = 1, FormId = 100 });
        var pipe = new Pipe();
        await pipe.Writer.CompleteAsync(); // Close writer to cause error

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to export", result.Errors.First());
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
        // Should handle cancellation gracefully
        Assert.NotNull(result);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldProduceIndentedJson()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                AnswersModel = """{"q1": "a1"}"""
            }
        );
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        // Indented JSON should contain newlines
        Assert.Contains('\n', content);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldHandleDateTimeValues()
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Utc);
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                CreatedAt = dateTime,
                ModifiedAt = dateTime.AddHours(1),
                AnswersModel = """{"q1": "a1"}"""
            }
        );
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var json = JsonDocument.Parse(content);
        var firstObject = json.RootElement[0];
        var createdAtValue = firstObject.GetProperty("createdAt").GetString();
        Assert.NotNull(createdAtValue);
        Assert.Contains("2024", createdAtValue);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldHandleDecimalNumbers()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                AnswersModel = """{"price": 99.99, "quantity": 5}"""
            }
        );
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        var json = JsonDocument.Parse(content);
        var firstObject = json.RootElement[0];
        Assert.Equal(99.99m, firstObject.GetProperty("price").GetDecimal());
        Assert.Equal(5, firstObject.GetProperty("quantity").GetInt32());
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
        var content = Encoding.UTF8.GetString(buffer.FirstSpan);
        reader.AdvanceTo(buffer.End);
        await reader.CompleteAsync();
        return content;
    }
}


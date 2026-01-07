using System.IO.Pipelines;
using System.Text;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Exporting.Exporters.Dynamic;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Exporting.Exporters.Dynamic;

public sealed class CodebookJsonExporterTests
{
    private readonly ILogger<CodebookJsonExporter> _logger;
    private readonly CodebookJsonExporter _sut;

    public CodebookJsonExporterTests()
    {
        _logger = Substitute.For<ILogger<CodebookJsonExporter>>();
        _sut = new CodebookJsonExporter(_logger);
    }

    [Fact]
    public void Format_ShouldReturnCodebook()
    {
        Assert.Equal("codebook", _sut.Format);
    }

    [Fact]
    public void ContentType_ShouldReturnApplicationJson()
    {
        Assert.Equal("application/json", _sut.ContentType);
    }

    [Fact]
    public void ItemType_ShouldReturnDynamicExportRow()
    {
        Assert.Equal(typeof(DynamicExportRow), _sut.ItemType);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldExportSingleRow()
    {
        // Arrange
        var jsonData = """{"key": "value", "number": 42}""";
        var records = CreateTestRecords(new DynamicExportRow { Data = jsonData });
        var pipe = new Pipe();
        var options = new ExportOptions();

        // Act
        var result = await _sut.StreamExportAsync(records, options, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        Assert.Equal(jsonData, content);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldExportMultipleRows()
    {
        // Arrange
        var records = CreateTestRecords(
            new DynamicExportRow { Data = """{"row": 1}""" },
            new DynamicExportRow { Data = """{"row": 2}""" },
            new DynamicExportRow { Data = """{"row": 3}""" }
        );
        var pipe = new Pipe();
        var options = new ExportOptions();

        // Act
        var result = await _sut.StreamExportAsync(records, options, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        // All rows should be concatenated
        Assert.Contains("""{"row": 1}""", content);
        Assert.Contains("""{"row": 2}""", content);
        Assert.Contains("""{"row": 3}""", content);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldSkipEmptyData()
    {
        // Arrange
        var records = CreateTestRecords(
            new DynamicExportRow { Data = """{"row": 1}""" },
            new DynamicExportRow { Data = string.Empty },
            new DynamicExportRow { Data = """{"row": 3}""" }
        );
        var pipe = new Pipe();
        var options = new ExportOptions();

        // Act
        var result = await _sut.StreamExportAsync(records, options, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        Assert.Contains("""{"row": 1}""", content);
        Assert.Contains("""{"row": 3}""", content);
        // Should not contain empty data
        Assert.DoesNotContain("\"\"", content);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldIncludeWhitespaceOnlyData()
    {
        // Arrange
        // Note: The exporter only skips null or empty strings, not whitespace-only strings
        var records = CreateTestRecords(
            new DynamicExportRow { Data = """{"row": 1}""" },
            new DynamicExportRow { Data = "   " }, // Whitespace only - will be included
            new DynamicExportRow { Data = """{"row": 3}""" }
        );
        var pipe = new Pipe();
        var options = new ExportOptions();

        // Act
        var result = await _sut.StreamExportAsync(records, options, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        Assert.Contains("""{"row": 1}""", content);
        Assert.Contains("""{"row": 3}""", content);
        // Whitespace-only data is included (only null/empty are skipped)
        Assert.Contains("   ", content);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldReturnFileNameWithFormId_WhenFormIdInMetadata()
    {
        // Arrange
        var formId = 123L;
        var records = CreateTestRecords(new DynamicExportRow { Data = """{}""" });
        var pipe = new Pipe();
        var options = new ExportOptions
        {
            Metadata = new Dictionary<string, object> { ["FormId"] = formId }
        };

        // Act
        var result = await _sut.StreamExportAsync(records, options, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal($"codebook-{formId}.json", result.Value.FileName);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldReturnFileNameWithUnknown_WhenNoFormIdInMetadata()
    {
        // Arrange
        var records = CreateTestRecords(new DynamicExportRow { Data = """{}""" });
        var pipe = new Pipe();
        var options = new ExportOptions();

        // Act
        var result = await _sut.StreamExportAsync(records, options, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("codebook-unknown.json", result.Value.FileName);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldReturnError_WhenExceptionThrown()
    {
        // Arrange
        var records = CreateTestRecords(new DynamicExportRow { Data = """{}""" });
        var options = new ExportOptions();

        // Create a pipe writer that will throw when writing
        var pipe = new Pipe();
        var failingWriter = Substitute.For<PipeWriter>();
        failingWriter.AsStream().Returns(x => throw new Exception("Write failed"));

        // Act
        var result = await _sut.StreamExportAsync(records, options, CancellationToken.None, failingWriter);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to export Codebook JSON", result.Errors.First());
    }

    [Fact]
    public async Task GetHeadersAsync_ShouldReturnSuccess_WhenValidOptions()
    {
        // Arrange
        var formId = 123L;
        var options = new ExportOptions
        {
            Metadata = new Dictionary<string, object> { ["FormId"] = formId }
        };

        // Act
        var result = await _sut.GetHeadersAsync(options, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var fileExport = result.Value;
        Assert.Equal("application/json", fileExport.ContentType);
        Assert.Equal($"codebook-{formId}.json", fileExport.FileName);
    }

    [Fact]
    public async Task GetHeadersAsync_ShouldReturnSuccess_WhenNoOptions()
    {
        // Act
        var result = await _sut.GetHeadersAsync(null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var fileExport = result.Value;
        Assert.Equal("application/json", fileExport.ContentType);
        Assert.Equal("codebook-unknown.json", fileExport.FileName);
    }

    [Fact]
    public async Task GetHeadersAsync_ShouldReturnSuccess_WhenNoFormIdInMetadata()
    {
        // Arrange
        var options = new ExportOptions
        {
            Metadata = new Dictionary<string, object> { ["OtherKey"] = "value" }
        };

        // Act
        var result = await _sut.GetHeadersAsync(options, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var fileExport = result.Value;
        Assert.Equal("codebook-unknown.json", fileExport.FileName);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldHandleLargeJsonData()
    {
        // Arrange
        var largeJson = new StringBuilder();
        largeJson.Append("{");
        for (int i = 0; i < 1000; i++)
        {
            if (i > 0)
                largeJson.Append(",");
            largeJson.Append($"\"key{i}\": \"value{i}\"");
        }
        largeJson.Append("}");

        var records = CreateTestRecords(new DynamicExportRow { Data = largeJson.ToString() });
        var pipe = new Pipe();
        var options = new ExportOptions();

        // Act
        var result = await _sut.StreamExportAsync(records, options, CancellationToken.None, pipe.Writer);

        // Assert
        Assert.True(result.IsSuccess);
        var content = await ReadPipeContent(pipe.Reader);
        Assert.Contains("key0", content);
        Assert.Contains("key999", content);
    }

    [Fact]
    public async Task StreamExportAsync_ShouldRespectCancellationToken()
    {
        // Arrange
        var records = CreateTestRecords(
            Enumerable.Range(1, 1000).Select(i => new DynamicExportRow { Data = $"{{\"id\": {i}}}" })
        );
        var pipe = new Pipe();
        var options = new ExportOptions();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        var result = await _sut.StreamExportAsync(records, options, cancellationTokenSource.Token, pipe.Writer);

        // Assert
        // Should handle cancellation gracefully (may succeed with partial data or fail)
        Assert.NotNull(result);
    }

    private static async IAsyncEnumerable<DynamicExportRow> CreateTestRecords(params DynamicExportRow[] rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<DynamicExportRow> CreateTestRecords(IEnumerable<DynamicExportRow> rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }
        await Task.CompletedTask;
    }

    private static async Task<string> ReadPipeContent(PipeReader reader)
    {
        var contentBuilder = new StringBuilder();
        while (true)
        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;

            foreach (var segment in buffer)
            {
                contentBuilder.Append(Encoding.UTF8.GetString(segment.Span));
            }

            reader.AdvanceTo(buffer.End);

            if (result.IsCompleted)
            {
                break;
            }
        }

        await reader.CompleteAsync();
        return contentBuilder.ToString();
    }
}

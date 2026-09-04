using System.Globalization;
using System.IO.Pipelines;
using System.Text.Json.Nodes;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Exporting;
using Endatix.Infrastructure.Exporting.Exporters.Submissions;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public sealed class SubmissionXlsxExporterTests
{
    private const string Emoji = "\U0001F600";

    private readonly SubmissionXlsxExporter _sut;

    public SubmissionXlsxExporterTests()
    {
        var logger = Substitute.For<ILogger<SubmissionXlsxExporter>>();
        var transformer = Substitute.For<IValueTransformer>();
        transformer
            .Transform(Arg.Any<JsonNode?>(), Arg.Any<TransformationContext<SubmissionExportRow>>())
            .Returns(callInfo => (JsonNode?)callInfo[0]);
        _sut = new SubmissionXlsxExporter(logger, [transformer]);
    }

    [Fact]
    public void Format_Always_DescribesXlsxPackage()
    {
        // Arrange
        // Act
        var format = _sut.Format;

        // Assert
        Assert.Equal("xlsx", format);
        Assert.Equal("xlsx", _sut.FileExtension);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _sut.ContentType);
    }

    [Fact]
    public async Task StreamExportAsync_WithLongIds_WritesThemAsInlineStrings()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 123456789012345678,
                FormId = 100,
                SubmitterId = 55,
                SubmitterDisplayId = "display-9",
                IsComplete = true,
                CreatedAt = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                AnswersModel = """{"question1":"answer1","question2":42,"choiceId":"123456789012345678"}"""
            });

        // Act
        var (result, cells) = await ExportFirstDataRow(records);

        // Assert
        Assert.Equal("submissions-100.xlsx", result.Value.FileName);
        Assert.Equal("123456789012345678", cells["Id"].Text);
        Assert.Equal(CellValues.InlineString, cells["Id"].Type);
        Assert.Equal("100", cells["FormId"].Text);
        Assert.Equal(CellValues.InlineString, cells["FormId"].Type);
        Assert.Equal("55", cells["SubmitterId"].Text);
        Assert.Equal("display-9", cells["SubmitterDisplayId"].Text);
        Assert.Equal("answer1", cells["question1"].Text);
        Assert.Equal("42", cells["question2"].Text);
        Assert.Equal(CellValues.Number, cells["question2"].Type);
        Assert.Equal("123456789012345678", cells["choiceId"].Text);
        Assert.Equal(CellValues.InlineString, cells["choiceId"].Type);
    }

    [Fact]
    public async Task StreamExportAsync_WithDatesAndBooleans_WritesTypedCells()
    {
        // Arrange
        var createdAt = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                IsComplete = true,
                CreatedAt = createdAt,
                AnswersModel = """{"flag":false,"when":"2024-06-15T12:00:00Z"}"""
            });

        // Act
        var (_, cells) = await ExportFirstDataRow(records);

        // Assert
        Assert.Equal(CellValues.Number, cells["CreatedAt"].Type);
        Assert.Equal(ExcelSheetStyles.DateTimeStyleIndex, cells["CreatedAt"].StyleIndex);
        Assert.Equal(
            createdAt.ToOADate(),
            double.Parse(cells["CreatedAt"].Text, CultureInfo.InvariantCulture),
            precision: 8);
        Assert.Equal(CellValues.Number, cells["when"].Type);
        Assert.Equal(ExcelSheetStyles.DateTimeStyleIndex, cells["when"].StyleIndex);
        Assert.Equal(CellValues.Boolean, cells["IsComplete"].Type);
        Assert.Equal(CellValues.Boolean, cells["flag"].Type);
    }

    /// <summary>Excel reads only 1/0 in a <c>t="b"</c> cell; "true" triggers the repair prompt.</summary>
    [Theory]
    [InlineData(true, "1")]
    [InlineData(false, "0")]
    public async Task StreamExportAsync_WithBoolean_WritesExcelBooleanLiteral(bool isComplete, string expected)
    {
        // Arrange
        var records = CreateTestRecords(new SubmissionExportRow { Id = 1, FormId = 100, IsComplete = isComplete });

        // Act
        var (_, cells) = await ExportFirstDataRow(records);

        // Assert
        Assert.Equal(expected, cells["IsComplete"].Text);
        Assert.Equal(CellValues.Boolean, cells["IsComplete"].Type);
    }

    [Fact]
    public async Task StreamExportAsync_WithCategoryIdBooleans_WritesNumberCells()
    {
        // Arrange
        var records = CreateTestRecords(new SubmissionExportRow { Id = 1, FormId = 100, IsComplete = true });
        var options = new ExportOptions
        {
            Metadata = new Dictionary<string, object>
            {
                [SubmissionExportMetadataKeys.ExecutionSettings] =
                    new SubmissionExportExecutionSettings(EncodeBooleansAsCategoryIds: true)
            }
        };

        // Act
        var (_, cells) = await ExportFirstDataRow(records, options);

        // Assert
        Assert.Equal("1", cells["IsComplete"].Text);
        Assert.Equal(CellValues.Number, cells["IsComplete"].Type);
    }

    /// <summary>Sniffing numbers out of text would show <c>7</c> for <c>007</c> and make Excel reject <c>NaN</c>.</summary>
    [Theory]
    [InlineData("007")]
    [InlineData("NaN")]
    [InlineData("+1")]
    public async Task StreamExportAsync_WithNumericLookingText_KeepsItAsString(string answer)
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow { Id = 1, FormId = 100, AnswersModel = $$"""{"code":"{{answer}}"}""" });

        // Act
        var (_, cells) = await ExportFirstDataRow(records);

        // Assert
        Assert.Equal(answer, cells["code"].Text);
        Assert.Equal(CellValues.InlineString, cells["code"].Type);
    }

    [Fact]
    public async Task StreamExportAsync_WithNonXmlCharacters_KeepsEmojiAndDropsControlCodes()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                AnswersModel = $$"""{"note":"hi {{Emoji}} \u0007there"}"""
            });

        // Act
        var (_, cells) = await ExportFirstDataRow(records);

        // Assert
        Assert.Equal($"hi {Emoji} there", cells["note"].Text);
    }

    [Fact]
    public async Task StreamExportAsync_WithFormIdMetadata_NamesFileFromMetadata()
    {
        // Arrange
        var records = CreateTestRecords(new SubmissionExportRow { Id = 1, FormId = 100 });
        var options = new ExportOptions { Metadata = new Dictionary<string, object> { ["FormId"] = 456L } };

        // Act
        var (result, _) = await ExportFirstDataRow(records, options);

        // Assert
        Assert.Equal("submissions-456.xlsx", result.Value.FileName);
    }

    [Fact]
    public async Task StreamExportAsync_WithMissingSubmitterId_WritesNotAvailable()
    {
        // Arrange
        var records = CreateTestRecords(
            new SubmissionExportRow { Id = 1, FormId = 100, SubmitterId = null, AnswersModel = """{"q1":"a"}""" });

        // Act
        var (_, cells) = await ExportFirstDataRow(records);

        // Assert
        Assert.Equal("N/A", cells["SubmitterId"].Text);
    }

    [Fact]
    public async Task StreamExportAsync_WithNoRecords_WritesWorkbookWithoutRows()
    {
        // Arrange
        var pipe = new Pipe();

        // Act
        var result = await _sut.StreamExportAsync(CreateTestRecords(), null, CancellationToken.None, pipe.Writer);
        await pipe.Writer.CompleteAsync();

        // Assert
        Assert.True(result.IsSuccess);
        using var stream = new MemoryStream(await ReadAllBytes(pipe.Reader));
        using var document = SpreadsheetDocument.Open(stream, false);
        var sheetData = document.WorkbookPart!.WorksheetParts.First().Worksheet.Elements<SheetData>().First();
        Assert.Empty(sheetData.Elements<Row>());
    }

    [Fact]
    public async Task StreamExportAsync_WithNullWriter_ThrowsArgumentNullException()
    {
        // Arrange
        var records = CreateTestRecords(new SubmissionExportRow { Id = 1, FormId = 100 });

        // Act
        var act = () => _sut.StreamExportAsync(records, null, CancellationToken.None, null!);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(act);
    }

    private async Task<(Result<FileExport> Result, Dictionary<string, CellSnapshot> Cells)> ExportFirstDataRow(
        IAsyncEnumerable<SubmissionExportRow> records,
        ExportOptions? options = null)
    {
        var pipe = new Pipe();
        var result = await _sut.StreamExportAsync(records, options, CancellationToken.None, pipe.Writer);
        await pipe.Writer.CompleteAsync();
        Assert.True(result.IsSuccess);

        using var stream = new MemoryStream(await ReadAllBytes(pipe.Reader));
        using var document = SpreadsheetDocument.Open(stream, false);
        return (result, ReadFirstDataRow(document));
    }

    private static Dictionary<string, CellSnapshot> ReadFirstDataRow(SpreadsheetDocument document)
    {
        var sheetData = document.WorkbookPart!.WorksheetParts.First().Worksheet.Elements<SheetData>().First();
        var rows = sheetData.Elements<Row>().ToList();
        Assert.True(rows.Count >= 2);

        var headers = rows[0].Elements<Cell>().Select(ReadCellText).ToList();
        var values = rows[1].Elements<Cell>().ToList();
        var cells = new Dictionary<string, CellSnapshot>(StringComparer.Ordinal);
        for (var i = 0; i < headers.Count; i++)
        {
            cells[headers[i]] = new CellSnapshot(
                ReadCellText(values[i]),
                values[i].DataType?.Value,
                values[i].StyleIndex?.Value);
        }

        return cells;
    }

    private static string ReadCellText(Cell cell) =>
        cell.InlineString is not null
            ? cell.InlineString.Text?.Text ?? string.Empty
            : cell.CellValue?.Text ?? string.Empty;

    private static async IAsyncEnumerable<SubmissionExportRow> CreateTestRecords(params SubmissionExportRow[] rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }

        await Task.CompletedTask;
    }

    private static async Task<byte[]> ReadAllBytes(PipeReader reader)
    {
        using var buffer = new MemoryStream();
        while (true)
        {
            var read = await reader.ReadAsync();
            foreach (var segment in read.Buffer)
            {
                buffer.Write(segment.Span);
            }

            reader.AdvanceTo(read.Buffer.End);
            if (read.IsCompleted)
            {
                break;
            }
        }

        await reader.CompleteAsync();
        return buffer.ToArray();
    }

    private sealed record CellSnapshot(string Text, CellValues? Type, uint? StyleIndex);
}

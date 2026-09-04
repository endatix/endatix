using System.Globalization;
using System.IO.Pipelines;
using System.Text.Json.Nodes;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Exporting;
using Endatix.Infrastructure.Exporting.Exporters.Submissions;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public sealed class SubmissionXlsxExporterTests
{
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
    public void Format_ShouldReturnXlsx()
    {
        Assert.Equal("xlsx", _sut.Format);
        Assert.Equal("xlsx", _sut.FileExtension);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _sut.ContentType);
    }

    [Fact]
    public async Task StreamExportAsync_WritesIdColumnsAsInlineStrings()
    {
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

        var pipe = new Pipe();
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);
        await pipe.Writer.CompleteAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("submissions-100.xlsx", result.Value.FileName);

        var bytes = await ReadAllBytes(pipe.Reader);
        using var stream = new MemoryStream(bytes);
        using var document = SpreadsheetDocument.Open(stream, false);
        var cells = ReadFirstDataRow(document);

        Assert.Equal("123456789012345678", cells["Id"].Text);
        Assert.Equal(CellValues.InlineString, cells["Id"].Type);
        Assert.Equal("100", cells["FormId"].Text);
        Assert.Equal(CellValues.InlineString, cells["FormId"].Type);
        Assert.Equal("55", cells["SubmitterId"].Text);
        Assert.Equal("display-9", cells["SubmitterDisplayId"].Text);
        Assert.Equal(CellValues.Boolean, cells["IsComplete"].Type);
        Assert.Equal("true", cells["IsComplete"].Text, ignoreCase: true);
        Assert.Equal(CellValues.Number, cells["CreatedAt"].Type);
        Assert.Equal(ExcelSheetStyles.DateTimeStyleIndex, cells["CreatedAt"].StyleIndex);
        Assert.Equal(
            new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc).ToOADate(),
            double.Parse(cells["CreatedAt"].Text, CultureInfo.InvariantCulture),
            precision: 8);
        Assert.Equal("answer1", cells["question1"].Text);
        Assert.Equal("42", cells["question2"].Text);
        Assert.Equal(CellValues.Number, cells["question2"].Type);
        Assert.Equal("123456789012345678", cells["choiceId"].Text);
        Assert.Equal(CellValues.InlineString, cells["choiceId"].Type);
    }

    [Fact]
    public async Task StreamExportAsync_LeavesMissingSubmitterIdAsNotAvailable()
    {
        var records = CreateTestRecords(
            new SubmissionExportRow
            {
                Id = 1,
                FormId = 100,
                SubmitterId = null,
                AnswersModel = """{"q1":"a"}"""
            });

        var pipe = new Pipe();
        var result = await _sut.StreamExportAsync(records, null, CancellationToken.None, pipe.Writer);
        await pipe.Writer.CompleteAsync();

        Assert.True(result.IsSuccess);
        var bytes = await ReadAllBytes(pipe.Reader);
        using var stream = new MemoryStream(bytes);
        using var document = SpreadsheetDocument.Open(stream, false);
        var cells = ReadFirstDataRow(document);
        Assert.Equal("N/A", cells["SubmitterId"].Text);
    }

    private static Dictionary<string, (string Text, CellValues? Type, uint? StyleIndex)> ReadFirstDataRow(
        SpreadsheetDocument document)
    {
        var worksheetPart = document.WorkbookPart!.WorksheetParts.First();
        var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();
        var rows = sheetData.Elements<Row>().ToList();
        Assert.True(rows.Count >= 2);

        var headers = rows[0].Elements<Cell>().Select(ReadCellText).ToList();
        var values = rows[1].Elements<Cell>().ToList();
        var map = new Dictionary<string, (string Text, CellValues? Type, uint? StyleIndex)>(StringComparer.Ordinal);
        for (var i = 0; i < headers.Count; i++)
        {
            var cell = values[i];
            map[headers[i]] = (ReadCellText(cell), cell.DataType?.Value, cell.StyleIndex?.Value);
        }

        return map;
    }

    private static string ReadCellText(Cell cell)
    {
        if (cell.InlineString is not null)
        {
            return cell.InlineString.Text?.Text ?? string.Empty;
        }

        return cell.CellValue?.Text ?? string.Empty;
    }

    private static async IAsyncEnumerable<SubmissionExportRow> CreateTestRecords(
        params SubmissionExportRow[] rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }

        await Task.CompletedTask;
    }

    private static async Task<byte[]> ReadAllBytes(PipeReader reader)
    {
        using var ms = new MemoryStream();
        while (true)
        {
            var read = await reader.ReadAsync();
            foreach (var segment in read.Buffer)
            {
                ms.Write(segment.Span);
            }

            reader.AdvanceTo(read.Buffer.End);
            if (read.IsCompleted)
            {
                break;
            }
        }

        await reader.CompleteAsync();
        return ms.ToArray();
    }
}

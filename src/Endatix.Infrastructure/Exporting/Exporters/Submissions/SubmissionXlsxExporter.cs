using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Xml;
using Ardalis.GuardClauses;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Exporting.ColumnDefinitions;
using Endatix.Infrastructure.Exporting.Formatters;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Exporting.Exporters.Submissions;

/// <summary>
/// Flat XLSX export of submission rows. ID cells are stored as text so Excel keeps full snowflake IDs.
/// </summary>
public sealed class SubmissionXlsxExporter(
    ILogger<SubmissionXlsxExporter> logger,
    IEnumerable<IValueTransformer> globalTransformers) : SubmissionExporterBase(logger, globalTransformers)
{
    private const string SheetName = "Submissions";
    private const int FileBufferSize = 64 * 1024;

    public override string Format => "xlsx";
    public override string ContentType =>
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public override string FileExtension => "xlsx";

    public override async Task<Result<FileExport>> StreamExportAsync(
        IAsyncEnumerable<SubmissionExportRow> records,
        ExportOptions? options,
        CancellationToken cancellationToken,
        PipeWriter writer)
    {
        Guard.Against.Null(writer);

        // OpenXml needs a seekable stream and the response pipe is not one, so the package is
        // built in a private temp directory (0700) and streamed out once complete.
        var tempDirectory = Directory.CreateTempSubdirectory("endatix-xlsx-");
        try
        {
            SubmissionExportRow? firstRow;
            await using (var file = new FileStream(
                Path.Combine(tempDirectory.FullName, "workbook.xlsx"),
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                FileBufferSize,
                FileOptions.DeleteOnClose))
            {
                firstRow = await WriteWorkbookAsync(file, records, options, cancellationToken);
                file.Position = 0;

                await using var output = writer.AsStream(leaveOpen: true);
                await file.CopyToAsync(output, cancellationToken);
            }

            await writer.FlushAsync(cancellationToken);
            return Result<FileExport>.Success(
                new FileExport(ContentType, GetFileName(options, firstRow, FileExtension)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting submissions to XLSX");
            return Result<FileExport>.Error("Failed to export submissions.");
        }
        finally
        {
            TryDelete(tempDirectory);
        }
    }

    // Cleanup must never turn a finished export into a failed response.
    private void TryDelete(DirectoryInfo directory)
    {
        try
        {
            directory.Delete(recursive: true);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not delete XLSX scratch directory {Directory}", directory.FullName);
        }
    }

    [SuppressMessage(
        "Major Code Smell",
        "S6966:Awaitable method should be used",
        Justification = "OpenXmlWriter.Create builds its XmlWriter without XmlWriterSettings.Async, " +
                        "so every Write*Async overload throws InvalidOperationException at runtime. " +
                        "Writes target a local temp file; only the copy to the response pipe is async.")]
    private async Task<SubmissionExportRow?> WriteWorkbookAsync(
        Stream stream,
        IAsyncEnumerable<SubmissionExportRow> records,
        ExportOptions? options,
        CancellationToken cancellationToken)
    {
        using var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        ExcelSheetStyles.AddDefaultStyles(workbookPart);
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();

        var categoryIdBooleans = UsesCategoryIdBooleans(options);
        var formatter = new DefaultCsvFormatter(categoryIdBooleans);
        SubmissionExportRow? firstRow = null;

        using (var xmlWriter = OpenXmlWriter.Create(worksheetPart))
        {
            xmlWriter.WriteStartElement(new Worksheet());
            xmlWriter.WriteStartElement(new SheetData());

            var headerWritten = false;
            await foreach ((var row, var doc, var columns) in GetStreamContextAsync(records, options, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (doc)
                {
                    firstRow ??= row;

                    if (!headerWritten)
                    {
                        WriteHeaderRow(xmlWriter, columns);
                        headerWritten = true;
                    }

                    WriteDataRow(xmlWriter, row, doc, columns, formatter, categoryIdBooleans);
                }
            }

            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndElement();
        }

        workbookPart.Workbook
            .AppendChild(new Sheets())
            .AppendChild(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = SheetName
            });
        workbookPart.Workbook.Save();
        return firstRow;
    }

    private static void WriteHeaderRow(
        OpenXmlWriter xmlWriter,
        List<ColumnDefinition<SubmissionExportRow>> columns)
    {
        xmlWriter.WriteStartElement(new Row());
        foreach (var col in columns)
        {
            WriteInlineString(xmlWriter, col.Name);
        }

        xmlWriter.WriteEndElement();
    }

    private void WriteDataRow(
        OpenXmlWriter xmlWriter,
        SubmissionExportRow row,
        JsonDocument? doc,
        List<ColumnDefinition<SubmissionExportRow>> columns,
        DefaultCsvFormatter formatter,
        bool categoryIdBooleans)
    {
        var context = new TransformationContext<SubmissionExportRow>(row, doc, _logger);
        xmlWriter.WriteStartElement(new Row());
        foreach (var col in columns)
        {
            try
            {
                var value = col.GetValue(context);
                WriteCell(xmlWriter, col.Name, value, formatter.Format(value, context), categoryIdBooleans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing column {ColumnName} for row {RowId}", col.Name, row.Id);
                WriteInlineString(xmlWriter, NOT_AVAILABLE_VALUE);
            }
        }

        xmlWriter.WriteEndElement();
    }

    /// <summary>
    /// Typed cells only where the unwrapped value carries a type Excel understands. Numeric-looking
    /// text (<c>007</c>, <c>NaN</c>) stays a string — guessing loses leading zeros and can emit a
    /// number cell Excel refuses to open.
    /// </summary>
    private static void WriteCell(
        OpenXmlWriter xmlWriter,
        string columnName,
        object? value,
        object? formatted,
        bool categoryIdBooleans)
    {
        var display = formatted?.ToString() ?? string.Empty;
        if (ExcelIdCell.ShouldWriteAsText(columnName, display))
        {
            WriteInlineString(xmlWriter, display);
            return;
        }

        switch (ExcelExportValue.Unwrap(value))
        {
            case DateTime dateTime:
                WriteDateTime(xmlWriter, dateTime);
                break;
            case DateTimeOffset dateTimeOffset:
                WriteDateTime(xmlWriter, dateTimeOffset.UtcDateTime);
                break;
            case bool boolean:
                WriteBoolean(xmlWriter, boolean, categoryIdBooleans);
                break;
            case IFormattable number when IsExcelNumber(number):
                WriteValueCell(xmlWriter, CellValues.Number, number.ToString(null, CultureInfo.InvariantCulture));
                break;
            default:
                WriteInlineString(xmlWriter, display);
                break;
        }
    }

    private static bool IsExcelNumber(object value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    // Excel accepts only 1/0 in a t="b" cell; "true"/"false" triggers the repair prompt.
    private static void WriteBoolean(OpenXmlWriter xmlWriter, bool value, bool categoryIdBooleans) =>
        WriteValueCell(
            xmlWriter,
            categoryIdBooleans ? CellValues.Number : CellValues.Boolean,
            value ? "1" : "0");

    private static void WriteDateTime(OpenXmlWriter xmlWriter, DateTime dateTime) =>
        WriteValueCell(
            xmlWriter,
            CellValues.Number,
            dateTime.ToOADate().ToString(CultureInfo.InvariantCulture),
            ExcelSheetStyles.DateTimeStyleIndex);

    private static void WriteValueCell(
        OpenXmlWriter xmlWriter,
        CellValues dataType,
        string value,
        uint? styleIndex = null)
    {
        xmlWriter.WriteStartElement(new Cell { DataType = dataType, StyleIndex = styleIndex });
        xmlWriter.WriteElement(new CellValue(value));
        xmlWriter.WriteEndElement();
    }

    private static void WriteInlineString(OpenXmlWriter xmlWriter, string value)
    {
        xmlWriter.WriteStartElement(new Cell { DataType = CellValues.InlineString });
        xmlWriter.WriteStartElement(new InlineString());
        xmlWriter.WriteElement(new Text(SanitizeXmlText(value)) { Space = SpaceProcessingModeValues.Preserve });
        xmlWriter.WriteEndElement();
        xmlWriter.WriteEndElement();
    }

    /// <summary>
    /// Drops characters XML 1.0 forbids (control codes, lone surrogates). Answers routinely carry
    /// emoji, so valid surrogate pairs must survive — <see cref="XmlConvert.IsXmlChar"/> rejects
    /// each half on its own.
    /// </summary>
    private static string SanitizeXmlText(string value)
    {
        if (value.All(XmlConvert.IsXmlChar))
        {
            return value;
        }

        var sanitized = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsSurrogatePair(value, i))
            {
                sanitized.Append(value[i]).Append(value[i + 1]);
                i++;
            }
            else if (XmlConvert.IsXmlChar(value[i]))
            {
                sanitized.Append(value[i]);
            }
        }

        return sanitized.ToString();
    }
}

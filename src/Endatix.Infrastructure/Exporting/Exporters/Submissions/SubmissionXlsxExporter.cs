using System.Globalization;
using System.IO.Pipelines;
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

        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"endatix-xlsx-{Guid.NewGuid():N}.xlsx");
            try
            {
                SubmissionExportRow? firstRow;
                await using (var file = new FileStream(
                    tempPath,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 64 * 1024,
                    options: FileOptions.Asynchronous | FileOptions.DeleteOnClose))
                {
                    firstRow = await WriteWorkbookAsync(file, records, options, cancellationToken);
                    file.Position = 0;
                    await using (var output = writer.AsStream(leaveOpen: true))
                    {
                        await file.CopyToAsync(output, cancellationToken);
                    }
                }

                await writer.FlushAsync(cancellationToken);
                return Result<FileExport>.Success(
                    new FileExport(ContentType, GetFileName(options, firstRow, FileExtension)));
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
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
    }

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
        var formatter = ResolveFormatter(options);
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

                    WriteDataRow(xmlWriter, row, doc, columns, formatter);
                }
            }

            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndElement();
        }

        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = "Submissions"
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
        System.Text.Json.JsonDocument? doc,
        List<ColumnDefinition<SubmissionExportRow>> columns,
        DefaultCsvFormatter formatter)
    {
        var context = new TransformationContext<SubmissionExportRow>(row, doc, _logger);
        xmlWriter.WriteStartElement(new Row());
        foreach (var col in columns)
        {
            try
            {
                var raw = col.GetValue(context);
                WriteCell(xmlWriter, col.Name, raw, formatter.Format(raw, context), formatter.EncodeBooleansAsCategoryIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing column {ColumnName} for row {RowId}", col.Name, row.Id);
                WriteInlineString(xmlWriter, NOT_AVAILABLE_VALUE);
            }
        }

        xmlWriter.WriteEndElement();
    }

    private static void WriteCell(
        OpenXmlWriter xmlWriter,
        string columnName,
        object? raw,
        object? formatted,
        bool encodeBooleansAsCategoryIds)
    {
        var display = formatted as string ?? formatted?.ToString() ?? string.Empty;
        if (ExcelIdCell.ShouldWriteAsText(columnName, display))
        {
            WriteInlineString(xmlWriter, display);
            return;
        }

        if (TryWriteTypedValue(xmlWriter, ExcelExportValue.Unwrap(raw), encodeBooleansAsCategoryIds))
        {
            return;
        }

        WriteFallbackCell(xmlWriter, formatted, display);
    }

    private static bool TryWriteTypedValue(
        OpenXmlWriter xmlWriter,
        object? value,
        bool encodeBooleansAsCategoryIds)
    {
        switch (value)
        {
            case null:
                WriteInlineString(xmlWriter, string.Empty);
                return true;
            case DateTime dateTime:
                WriteDateTime(xmlWriter, dateTime);
                return true;
            case DateTimeOffset dateTimeOffset:
                WriteDateTime(xmlWriter, dateTimeOffset.UtcDateTime);
                return true;
            case bool boolean:
                WriteBoolean(xmlWriter, boolean, encodeBooleansAsCategoryIds);
                return true;
            default:
                if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
                {
                    WriteNumber(xmlWriter, ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture));
                    return true;
                }

                return false;
        }
    }

    private static void WriteBoolean(OpenXmlWriter xmlWriter, bool value, bool encodeAsCategoryIds)
    {
        if (encodeAsCategoryIds)
        {
            WriteNumber(xmlWriter, value ? "1" : "0");
            return;
        }

        xmlWriter.WriteStartElement(new Cell { DataType = CellValues.Boolean });
        xmlWriter.WriteElement(new CellValue(value));
        xmlWriter.WriteEndElement();
    }

    private static void WriteFallbackCell(OpenXmlWriter xmlWriter, object? formatted, string text)
    {
        if (formatted is null)
        {
            WriteInlineString(xmlWriter, string.Empty);
            return;
        }

        if (formatted is not string and IFormattable formattable)
        {
            WriteNumber(xmlWriter, formattable.ToString(null, CultureInfo.InvariantCulture));
            return;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
            !text.Contains(' ', StringComparison.Ordinal))
        {
            WriteNumber(xmlWriter, text);
            return;
        }

        WriteInlineString(xmlWriter, text);
    }

    private static void WriteDateTime(OpenXmlWriter xmlWriter, DateTime dateTime)
    {
        xmlWriter.WriteStartElement(new Cell
        {
            DataType = CellValues.Number,
            StyleIndex = ExcelSheetStyles.DateTimeStyleIndex
        });
        xmlWriter.WriteElement(new CellValue(dateTime.ToOADate()));
        xmlWriter.WriteEndElement();
    }

    private static void WriteInlineString(OpenXmlWriter xmlWriter, string value)
    {
        var safe = SanitizeXmlText(value);
        xmlWriter.WriteStartElement(new Cell { DataType = CellValues.InlineString });
        xmlWriter.WriteStartElement(new InlineString());
        xmlWriter.WriteElement(new Text(safe) { Space = SpaceProcessingModeValues.Preserve });
        xmlWriter.WriteEndElement();
        xmlWriter.WriteEndElement();
    }

    private static void WriteNumber(OpenXmlWriter xmlWriter, string invariantNumber)
    {
        xmlWriter.WriteStartElement(new Cell { DataType = CellValues.Number });
        xmlWriter.WriteElement(new CellValue(invariantNumber));
        xmlWriter.WriteEndElement();
    }

    private static string SanitizeXmlText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        Span<char> buffer = value.Length <= 256 ? stackalloc char[value.Length] : new char[value.Length];
        var n = 0;
        foreach (var c in value)
        {
            if (XmlConvert.IsXmlChar(c))
            {
                buffer[n++] = c;
            }
        }

        return n == value.Length ? value : new string(buffer[..n]);
    }

    private DefaultCsvFormatter ResolveFormatter(ExportOptions? options)
    {
        if (options?.Metadata is not null &&
            options.Metadata.TryGetValue(SubmissionExportMetadataKeys.ExecutionSettings, out var settingsObject) &&
            settingsObject is SubmissionExportExecutionSettings executionSettings &&
            executionSettings.EncodeBooleansAsCategoryIds)
        {
            return new DefaultCsvFormatter(encodeBooleansAsCategoryIds: true);
        }

        return new DefaultCsvFormatter();
    }
}

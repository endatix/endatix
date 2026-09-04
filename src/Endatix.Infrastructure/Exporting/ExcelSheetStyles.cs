using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Endatix.Infrastructure.Exporting;

/// <summary>
/// Minimal stylesheet so Excel shows date serials as date-times (built-in formats are locale-dependent).
/// </summary>
internal static class ExcelSheetStyles
{
    public const uint DefaultStyleIndex = 0;
    public const uint DateTimeStyleIndex = 1;
    private const uint CustomDateTimeFormatId = 164;
    internal const string DateTimeFormatCode = "yyyy-mm-dd hh:mm:ss";

    public static void AddDefaultStyles(WorkbookPart workbookPart)
    {
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = new Stylesheet
        {
            NumberingFormats = new NumberingFormats(
                new NumberingFormat
                {
                    NumberFormatId = CustomDateTimeFormatId,
                    FormatCode = DateTimeFormatCode
                })
            { Count = 1 },
            Fonts = new Fonts(new Font()) { Count = 1 },
            Fills = new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }))
            { Count = 2 },
            Borders = new Borders(new Border()) { Count = 1 },
            CellStyleFormats = new CellStyleFormats(new CellFormat()) { Count = 1 },
            CellFormats = new CellFormats(
                new CellFormat
                {
                    FontId = 0,
                    FillId = 0,
                    BorderId = 0,
                    FormatId = 0
                },
                new CellFormat
                {
                    NumberFormatId = CustomDateTimeFormatId,
                    FontId = 0,
                    FillId = 0,
                    BorderId = 0,
                    FormatId = 0,
                    ApplyNumberFormat = true
                })
            { Count = 2 }
        };
        stylesPart.Stylesheet.Save();
    }
}

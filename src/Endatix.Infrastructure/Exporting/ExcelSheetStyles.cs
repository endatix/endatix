using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Endatix.Infrastructure.Exporting;

/// <summary>
/// Minimal stylesheet so Excel shows date serials as date-times (built-in formats are locale-dependent).
/// Children are passed as explicit arrays: the single-element <c>params</c> form is ambiguous with the
/// <c>IEnumerable&lt;OpenXmlElement&gt;</c> overload.
/// </summary>
internal static class ExcelSheetStyles
{
    public const uint DateTimeStyleIndex = 1;
    private const uint CustomDateTimeFormatId = 164;
    private const string DateTimeFormatCode = "yyyy-mm-dd hh:mm:ss";

    public static void AddDefaultStyles(WorkbookPart workbookPart)
    {
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = new Stylesheet
        {
            NumberingFormats = new NumberingFormats(new OpenXmlElement[]
            {
                new NumberingFormat
                {
                    NumberFormatId = CustomDateTimeFormatId,
                    FormatCode = DateTimeFormatCode
                }
            })
            { Count = 1 },
            Fonts = new Fonts(new OpenXmlElement[] { new Font() }) { Count = 1 },
            Fills = new Fills(new OpenXmlElement[]
            {
                new Fill(new OpenXmlElement[] { new PatternFill { PatternType = PatternValues.None } }),
                new Fill(new OpenXmlElement[] { new PatternFill { PatternType = PatternValues.Gray125 } })
            })
            { Count = 2 },
            Borders = new Borders(new OpenXmlElement[] { new Border() }) { Count = 1 },
            CellStyleFormats = new CellStyleFormats(new OpenXmlElement[] { new CellFormat() }) { Count = 1 },
            CellFormats = new CellFormats(new OpenXmlElement[]
            {
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
                }
            })
            { Count = 2 }
        };
        stylesPart.Stylesheet.Save();
    }
}

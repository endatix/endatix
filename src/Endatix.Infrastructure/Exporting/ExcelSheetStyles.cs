using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Endatix.Infrastructure.Exporting;

/// <summary>
/// Date serials as <c>yyyy-mm-dd hh:mm:ss</c> (built-in Excel formats are locale-dependent).
/// </summary>
internal static class ExcelSheetStyles
{
    /// <summary>Index into <c>cellXfs</c> of the date-time format below.</summary>
    public const uint DateTimeStyleIndex = 1;

    private const string StylesheetXml = """
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <numFmts count="1"><numFmt numFmtId="164" formatCode="yyyy-mm-dd hh:mm:ss"/></numFmts>
          <fonts count="1"><font/></fonts>
          <fills count="2">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
          </fills>
          <borders count="1"><border/></borders>
          <cellStyleXfs count="1"><xf/></cellStyleXfs>
          <cellXfs count="2">
            <xf fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
          </cellXfs>
        </styleSheet>
        """;

    public static void AddDefaultStyles(WorkbookPart workbookPart)
    {
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = new Stylesheet(StylesheetXml);
        stylesPart.Stylesheet.Save();
    }
}

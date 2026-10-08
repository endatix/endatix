using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Tests.Features.Import;

/// <summary>
/// The customer sample and the 5,000-row file share one header. Numbers are the
/// integer columns; everything else is text. businessid is the external id.
/// </summary>
public sealed class PanelCsvCompatibilityTests
{
    private static readonly string[] NumberHeaders = ["iddomicilio", "NSE_LOC", "edac", "ni", "idPainel"];

    [Theory]
    [InlineData("muestra-co.csv", 20)]
    [InlineData("audience-5000.csv", 5000)]
    public void Cells_MatchPlannedPropertyTypes(string fileName, int expectedRows)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Features", "Import", "Fixtures", fileName);
        using StreamReader reader = new(path);
        using CsvReader csv = new(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
        });
        csv.Read();
        csv.ReadHeader();
        string[] headers = csv.HeaderRecord!;
        headers.Should().Contain("businessid");

        int rows = 0;
        while (csv.Read())
        {
            rows++;
            foreach (string header in headers)
            {
                if (header == "businessid")
                {
                    csv.GetField(header).Should().NotBeNullOrWhiteSpace();
                    continue;
                }

                string value = csv.GetField(header) ?? "";
                Property property = new(new PropertyCreateArgs(1, 1, header, DataType(header), 0));
                PropertyValueRules.ValueError(property, value).Should().BeNull(because: $"{fileName} row {rows} {header}");
            }
        }

        rows.Should().Be(expectedRows);
    }

    private static string DataType(string header) =>
        NumberHeaders.Contains(header) ? AudienceDataTypeCodes.Number : AudienceDataTypeCodes.Text;
}

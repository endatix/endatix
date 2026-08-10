using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.DataLists.Translations;

namespace Endatix.Core.Tests.UseCases.DataLists.Translations;

public class DataListTranslationsCsvTests
{
    [Fact]
    public void Serialize_SimpleRows_WritesHeaderAndCrlfRows()
    {
        // Arrange
        IReadOnlyList<string> columns = ["default", "es"];
        DataListTranslationRow[] rows =
        [
            new("apple", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["default"] = "Apple",
                ["es"] = "Manzana"
            }),
            new("banana", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["default"] = "Banana"
            })
        ];

        // Act
        string csv = DataListTranslationsCsv.Serialize(columns, rows);

        // Assert
        csv.Should().Be(
            "value,default,es\r\n" +
            "apple,Apple,Manzana\r\n" +
            "banana,Banana,\r\n");
    }

    [Fact]
    public void Serialize_FieldsNeedingQuotes_EscapesCommasQuotesAndNewlines()
    {
        // Arrange
        IReadOnlyList<string> columns = ["default"];
        DataListTranslationRow[] rows =
        [
            new("a,b", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["default"] = "say \"hi\""
            }),
            new("nl", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["default"] = "line1\nline2"
            })
        ];

        // Act
        string csv = DataListTranslationsCsv.Serialize(columns, rows);

        // Assert
        csv.Should().Be(
            "value,default\r\n" +
            "\"a,b\",\"say \"\"hi\"\"\"\r\n" +
            "nl,\"line1\nline2\"\r\n");
    }

    [Fact]
    public void Parse_ValidDocument_ReturnsColumnsAndRows()
    {
        // Arrange
        const string csv =
            "value,default,es\r\n" +
            "apple,Apple,Manzana\r\n" +
            "banana,Banana,\r\n";

        // Act
        DataListTranslationsCsvDocument document = DataListTranslationsCsv.Parse(csv);

        // Assert
        document.Columns.Should().Equal("default", "es");
        document.Rows.Should().HaveCount(2);
        document.Rows[0].Value.Should().Be("apple");
        document.Rows[0].Labels.Should().ContainKey("default").WhoseValue.Should().Be("Apple");
        document.Rows[0].Labels.Should().ContainKey("es").WhoseValue.Should().Be("Manzana");
        document.Rows[1].Value.Should().Be("banana");
        document.Rows[1].Labels.Should().ContainKey("default").WhoseValue.Should().Be("Banana");
        document.Rows[1].Labels.Should().NotContainKey("es");
    }

    [Fact]
    public void Parse_RoundTrip_WithDefaultFrDe_PreservesValuesAndLabels()
    {
        // Arrange
        IReadOnlyList<string> columns = ["default", "fr", "de"];
        DataListTranslationRow[] rows =
        [
            new("apple", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["default"] = "Apple",
                ["fr"] = "Pomme",
                ["de"] = "Apfel"
            }),
            new("banana", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["default"] = "Banana",
                ["fr"] = "Banane"
            }),
            new("cherry", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["default"] = "Cherry, ripe",
                ["de"] = "Kirsche \"süß\""
            })
        ];

        // Act
        string csv = DataListTranslationsCsv.Serialize(columns, rows);
        DataListTranslationsCsvDocument document = DataListTranslationsCsv.Parse(csv);

        // Assert
        csv.Should().StartWith("value,default,fr,de\r\n");
        document.Columns.Should().Equal(columns);
        document.Rows.Should().HaveCount(3);
        document.Rows[0].Value.Should().Be("apple");
        document.Rows[0].Labels.Should().BeEquivalentTo(rows[0].Labels);
        document.Rows[1].Value.Should().Be("banana");
        document.Rows[1].Labels.Should().BeEquivalentTo(rows[1].Labels);
        document.Rows[1].Labels.Should().NotContainKey("de");
        document.Rows[2].Value.Should().Be("cherry");
        document.Rows[2].Labels.Should().BeEquivalentTo(rows[2].Labels);
        document.Rows[2].Labels.Should().NotContainKey("fr");
    }

    [Fact]
    public void Parse_EmptyCsv_ThrowsFormatException()
    {
        Action act = () => DataListTranslationsCsv.Parse(string.Empty);

        act.Should().Throw<FormatException>()
            .WithMessage("*header row starting with 'value'*");
    }

    [Fact]
    public void Parse_MissingValueColumn_ThrowsFormatException()
    {
        Action act = () => DataListTranslationsCsv.Parse("default,es\r\nApple,Manzana\r\n");

        act.Should().Throw<FormatException>()
            .WithMessage("*first CSV column must be 'value'*");
    }

    [Fact]
    public void Parse_RowCellCountMismatch_ThrowsFormatException()
    {
        Action act = () => DataListTranslationsCsv.Parse(
            "value,default,es\r\n" +
            "apple,Apple\r\n");

        act.Should().Throw<FormatException>()
            .WithMessage("*Row 2 has 2 cells but the header declares 3*");
    }

    [Fact]
    public void Parse_UnterminatedQuotedField_ThrowsFormatException()
    {
        Action act = () => DataListTranslationsCsv.Parse("value,default\r\n\"apple,Apple\r\n");

        act.Should().Throw<FormatException>()
            .WithMessage("*ends inside a quoted field*");
    }

    [Fact]
    public void Parse_BomPrefixedHeader_IsAccepted()
    {
        string csv = "\uFEFFvalue,default\r\napple,Apple\r\n";

        DataListTranslationsCsvDocument document = DataListTranslationsCsv.Parse(csv);

        document.Columns.Should().Equal("default");
        document.Rows.Should().ContainSingle()
            .Which.Value.Should().Be("apple");
    }

    [Fact]
    public void BuildColumns_PutsDefaultFirstThenAvailableCultures()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities", normalizedName: "cities");
        dataList.AddCulture(CultureCode.Parse("es"));
        dataList.AddCulture(CultureCode.Parse("fr"));

        IReadOnlyList<string> columns = DataListTranslationsCsv.BuildColumns(dataList);

        columns.Should().Equal(SurveyJsTranslationKeys.DefaultKey, "es", "fr");
    }
}

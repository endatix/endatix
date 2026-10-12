using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Import;

namespace Endatix.Modules.Audience.Tests.Features.Import;

public sealed class CsvColumnMapperTests
{
    private static readonly IReadOnlyList<Property> Department =
    [
        new(new PropertyCreateArgs(
            TenantId: 1,
            FormId: 10,
            Name: "Department",
            DataType: AudienceDataTypeCodes.Text,
            SortOrder: 0)),
    ];

    private static readonly IReadOnlyList<string> Headers = ["email", "Dept"];

    [Fact]
    public void MappingError_KnownPropertyAndColumn_ReturnsNull()
    {
        // Arrange
        Dictionary<string, string> columns = new() { ["department"] = "dept" };

        // Act
        string? error = CsvColumnMapper.MappingError(new CsvColumnRequest(Department, Headers, "email", columns));

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void MappingError_UnknownProperty_ReturnsMessage()
    {
        // Arrange
        Dictionary<string, string> columns = new() { ["departmnet"] = "Dept" };

        // Act
        string? error = CsvColumnMapper.MappingError(new CsvColumnRequest(Department, Headers, "email", columns));

        // Assert
        error.Should().Be("This form has no audience property 'departmnet'.");
    }

    [Fact]
    public void MappingError_ColumnMissingFromFile_ReturnsMessage()
    {
        // Arrange
        Dictionary<string, string> columns = new() { ["department"] = "Team" };

        // Act
        string? error = CsvColumnMapper.MappingError(new CsvColumnRequest(Department, Headers, "email", columns));

        // Assert
        error.Should().Be("The CSV has no 'Team' column.");
    }

    [Fact]
    public void MappingError_OneColumnForTwoProperties_ReturnsMessage()
    {
        // Arrange
        IReadOnlyList<Property> properties = [Text(1, "First name"), Text(2, "Display name")];
        Dictionary<string, string> columns = new() { ["first_name"] = "Name", ["display_name"] = "name" };

        // Act
        string? error = CsvColumnMapper.MappingError(new CsvColumnRequest(properties, ["email", "Name"], "email", columns));

        // Assert
        error.Should().Be("The CSV column 'Name' is mapped to more than one property.");
    }

    [Fact]
    public void ReadValues_ChoiceHeaderInAnotherCase_StoresThePropertysKey()
    {
        // Arrange
        CsvColumnMap map = ChoiceMap(AudienceDataTypeCodes.SingleChoice, """["North","South"]""", ["region__north"]);

        // Act
        CellRead read = CsvColumnMapper.ReadValues(map, Cells(("region__north", "1")));

        // Assert
        read.Error.Should().BeNull();
        read.Values.Should().Equal(new Dictionary<long, string> { [5] = "North" });
    }

    [Fact]
    public void ReadValues_TwoChoicesForASingleChoiceProperty_ReturnsError()
    {
        // Arrange
        CsvColumnMap map = ChoiceMap(
            AudienceDataTypeCodes.SingleChoice, """["North","South"]""", ["region__North", "region__South"]);

        // Act
        CellRead read = CsvColumnMapper.ReadValues(map, Cells(("region__North", "1"), ("region__South", "yes")));

        // Assert
        read.Error.Should().Be("'Region' takes one choice, but the row marks more than one.");
    }

    [Fact]
    public void ReadValues_MultiChoice_WritesJsonAsABrowserDoes()
    {
        // Arrange
        CsvColumnMap map = ChoiceMap(
            AudienceDataTypeCodes.MultipleChoice, """["Café","R&D"]""", ["region__Café", "region__R&D", "region__x"]);

        // Act
        CellRead read = CsvColumnMapper.ReadValues(
            map, Cells(("region__Café", "1"), ("region__R&D", "x"), ("region__x", "0")));

        // Assert
        read.Values[5].Should().Be("""["Café","R&D"]""");
    }

    private static Property Text(long id, string name) =>
        new(new PropertyCreateArgs(1, 10, name, AudienceDataTypeCodes.Text, 0)) { Id = id };

    private static CsvColumnMap ChoiceMap(string dataType, string choicesJson, IReadOnlyList<string> choiceHeaders)
    {
        Property region = new(new PropertyCreateArgs(1, 10, "Region", dataType, 0, choicesJson)) { Id = 5 };
        return CsvColumnMapper.Build(new CsvColumnRequest([region], ["email", .. choiceHeaders], "email", null));
    }

    private static Dictionary<string, string> Cells(params (string Header, string Value)[] cells) =>
        cells.ToDictionary(cell => cell.Header, cell => cell.Value, StringComparer.OrdinalIgnoreCase);
}

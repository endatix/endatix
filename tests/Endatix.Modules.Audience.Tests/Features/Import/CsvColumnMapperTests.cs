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

    [Theory]
    [InlineData("1")]
    [InlineData("TRUE")]
    [InlineData("Yes")]
    [InlineData("y")]
    [InlineData(" X ")]
    public void ReadValues_TickedCell_TicksTheChoice(string cell)
    {
        // Arrange
        CsvColumnMap map = BrandsMap(allowsOther: false, "brands__nike");

        // Act
        CellRead read = CsvColumnMapper.ReadValues(map, Cells(("brands__nike", cell)));

        // Assert
        read.Error.Should().BeNull();
        read.Values[5].Should().Be("""["nike"]""");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("False")]
    [InlineData("NO")]
    [InlineData("n")]
    [InlineData(" ")]
    public void ReadValues_UntickedCell_LeavesTheChoiceOut(string cell)
    {
        // Arrange
        CsvColumnMap map = BrandsMap(allowsOther: false, "brands__nike", "brands__adidas");

        // Act
        CellRead read = CsvColumnMapper.ReadValues(map, Cells(("brands__nike", cell), ("brands__adidas", "1")));

        // Assert
        read.Error.Should().BeNull();
        read.Values[5].Should().Be("""["adidas"]""");
    }

    [Fact]
    public void ReadValues_CellThatIsNeitherTickNorUntick_ReturnsErrorNamingTheColumn()
    {
        // Arrange
        CsvColumnMap map = BrandsMap(allowsOther: false, "brands__nike");

        // Act
        CellRead read = CsvColumnMapper.ReadValues(map, Cells(("brands__nike", "maybe")));

        // Assert
        read.Error.Should().Be("'brands__nike' must be 1 or 0 (true/false, yes/no, y/n and x also work), not 'maybe'.");
    }

    [Fact]
    public void ReadValues_SingleChoiceSplitColumnWithBadCell_ReturnsError()
    {
        // Arrange
        CsvColumnMap map = ChoiceMap(AudienceDataTypeCodes.SingleChoice, """["North","South"]""", ["region__North"]);

        // Act
        CellRead read = CsvColumnMapper.ReadValues(map, Cells(("region__North", "2")));

        // Assert
        read.Error.Should().Be("'region__North' must be 1 or 0 (true/false, yes/no, y/n and x also work), not '2'.");
    }

    [Fact]
    public void ReadValues_OtherTickedWithText_AddsTheTextAsAChoice()
    {
        // Arrange
        CsvColumnMap map = BrandsMap(allowsOther: true, "brands__nike", "brands__other", "brands__other_text");

        // Act
        CellRead read = CsvColumnMapper.ReadValues(
            map, Cells(("brands__nike", "1"), ("brands__other", "1"), ("brands__other_text", " Acme ")));

        // Assert
        read.Error.Should().BeNull();
        read.Values[5].Should().Be("""["nike","Acme"]""");
    }

    [Fact]
    public void ReadValues_OtherTextMatchingATickedChoice_StoresItOnce()
    {
        // Arrange
        CsvColumnMap map = BrandsMap(allowsOther: true, "brands__nike", "brands__other", "brands__other_text");

        // Act
        CellRead read = CsvColumnMapper.ReadValues(
            map, Cells(("brands__nike", "1"), ("brands__other", "1"), ("brands__other_text", "nike")));

        // Assert
        read.Values[5].Should().Be("""["nike"]""");
    }

    [Fact]
    public void ReadValues_OtherTextWithOtherUnticked_ReturnsError()
    {
        // Arrange
        CsvColumnMap map = BrandsMap(allowsOther: true, "brands__other", "brands__other_text");

        // Act
        CellRead read = CsvColumnMapper.ReadValues(map, Cells(("brands__other", "0"), ("brands__other_text", "Acme")));

        // Assert
        read.Error.Should().Be("'brands__other_text' has text, but 'brands__other' is not ticked.");
    }

    [Fact]
    public void ReadValues_OtherTextWithoutAnOtherColumn_ReturnsError()
    {
        // Arrange
        CsvColumnMap map = BrandsMap(allowsOther: true, "brands__other_text");

        // Act
        CellRead read = CsvColumnMapper.ReadValues(map, Cells(("brands__other_text", "Acme")));

        // Assert
        read.Error.Should().Be("'brands__other_text' has text, but 'brands__other' is not ticked.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ReadValues_OtherTickedWithoutText_ReturnsError(string text)
    {
        // Arrange
        CsvColumnMap map = BrandsMap(allowsOther: true, "brands__other", "brands__other_text");

        // Act
        CellRead read = CsvColumnMapper.ReadValues(map, Cells(("brands__other", "x"), ("brands__other_text", text)));

        // Assert
        read.Error.Should().Be("'brands__other' is ticked, but 'brands__other_text' is empty.");
    }

    [Fact]
    public void ReadValues_OtherTickedWithoutATextColumn_ReturnsError()
    {
        // Arrange
        CsvColumnMap map = BrandsMap(allowsOther: true, "brands__other");

        // Act
        CellRead read = CsvColumnMapper.ReadValues(map, Cells(("brands__other", "1")));

        // Assert
        read.Error.Should().Be("'brands__other' is ticked, but 'brands__other_text' is empty.");
    }

    [Fact]
    public void ReadValues_OtherTextOnAPropertyWithoutOther_ReturnsError()
    {
        // Arrange
        CsvColumnMap map = BrandsMap(allowsOther: false, "brands__other", "brands__other_text");

        // Act
        CellRead read = CsvColumnMapper.ReadValues(map, Cells(("brands__other", "1"), ("brands__other_text", "Acme")));

        // Assert
        read.Error.Should().Be("'brands__other_text' has text, but 'Brands' does not allow other choices.");
    }

    [Fact]
    public void ReadValues_OtherTickedOnAPropertyWithoutOther_StoresAKeyTheValueCheckRefuses()
    {
        // Arrange
        CsvColumnMap map = BrandsMap(allowsOther: false, "brands__other");

        // Act
        CellRead read = CsvColumnMapper.ReadValues(map, Cells(("brands__other", "1")));

        // Assert
        read.Error.Should().BeNull();
        read.Values[5].Should().Be("""["other"]""");
        map.PropertiesById[5].ValueError(read.Values[5]).Should().Be("'Brands' has no choice 'other'.");
    }

    [Fact]
    public void ReadValues_RealOtherChoiceWithoutText_StoresTheKey()
    {
        // Arrange
        CsvColumnMap map = ListedOtherMap(allowsOther: false, "region__other");

        // Act
        CellRead read = CsvColumnMapper.ReadValues(map, Cells(("region__other", "1")));

        // Assert
        read.Error.Should().BeNull();
        read.Values[5].Should().Be("""["other"]""");
    }

    [Fact]
    public void ReadValues_RealOtherChoiceThatAllowsOther_StoresTheKeyAndTheText()
    {
        // Arrange
        CsvColumnMap map = ListedOtherMap(allowsOther: true, "region__other", "region__other_text");

        // Act
        CellRead read = CsvColumnMapper.ReadValues(map, Cells(("region__other", "1"), ("region__other_text", "Acme")));

        // Assert
        read.Error.Should().BeNull();
        read.Values[5].Should().Be("""["other","Acme"]""");
    }

    private static Property Text(long id, string name) =>
        new(new PropertyCreateArgs(1, 10, name, AudienceDataTypeCodes.Text, 0)) { Id = id };

    private static CsvColumnMap ChoiceMap(string dataType, string choicesJson, IReadOnlyList<string> choiceHeaders) =>
        MapOf(new PropertyCreateArgs(1, 10, "Region", dataType, 0, choicesJson), choiceHeaders);

    /// <summary>A multiple-choice property that lists a key named <c>other</c>.</summary>
    private static CsvColumnMap ListedOtherMap(bool allowsOther, params string[] choiceHeaders) =>
        MapOf(
            new PropertyCreateArgs(
                1, 10, "Region", AudienceDataTypeCodes.MultipleChoice, 0, """["nike","other"]""", allowsOther),
            choiceHeaders);

    private static CsvColumnMap MapOf(PropertyCreateArgs args, IReadOnlyList<string> choiceHeaders)
    {
        Property property = new(args) { Id = 5 };
        return CsvColumnMapper.Build(new CsvColumnRequest([property], ["email", .. choiceHeaders], "email", null));
    }

    private static CsvColumnMap BrandsMap(bool allowsOther, params string[] choiceHeaders) =>
        MapOf(
            new PropertyCreateArgs(
                1, 10, "Brands", AudienceDataTypeCodes.MultipleChoice, 0, """["nike","adidas"]""", allowsOther),
            choiceHeaders);

    private static Dictionary<string, string> Cells(params (string Header, string Value)[] cells) =>
        cells.ToDictionary(cell => cell.Header, cell => cell.Value, StringComparer.OrdinalIgnoreCase);
}

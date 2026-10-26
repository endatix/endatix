using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Import;
using Endatix.Modules.Audience.Features.People;

namespace Endatix.Modules.Audience.Tests.Features.Import;

public sealed class ImportClearsTests
{
    private const long Email = 1;
    private const long City = 2;
    private const long Region = 3;
    private const long Age = 4;

    private static readonly IReadOnlyList<Property> Properties =
    [
        new(new PropertyCreateArgs(1, 10, "Email", AudienceDataTypeCodes.Text, 0)) { Id = Email },
        new(new PropertyCreateArgs(1, 10, "City", AudienceDataTypeCodes.Text, 1)) { Id = City },
        new(new PropertyCreateArgs(1, 10, "Region", AudienceDataTypeCodes.MultipleChoice, 2, """["North","South"]"""))
        {
            Id = Region,
        },
        new(new PropertyCreateArgs(1, 10, "Age", AudienceDataTypeCodes.Number, 3)) { Id = Age },
    ];

    // The file maps City, has Region's choice columns, and leaves Email (the match key) and Age out.
    private static readonly CsvColumnMap Map = CsvColumnMapper.Build(new CsvColumnRequest(
        Properties,
        ["Identifier", "Town", "region__North", "region__South"],
        "Identifier",
        new Dictionary<string, string> { ["city"] = "Town" }));

    private static readonly MatchKey MatchKey = new(Email, "Email");

    [Fact]
    public void Of_KeepBoth_ClearsNothing()
    {
        // Act
        IReadOnlySet<long> clears = ImportClears.Of(Map, AudienceImportOptions.Keep, MatchKey);

        // Assert
        clears.Should().BeEmpty();
    }

    [Fact]
    public void Of_ClearEmptyCells_ClearsTheMappedAndChoiceColumnProperties()
    {
        // Act
        IReadOnlySet<long> clears = ImportClears.Of(Map, new AudienceImportOptions(ClearEmptyCells: true, false), MatchKey);

        // Assert
        clears.Should().BeEquivalentTo([City, Region]);
    }

    [Fact]
    public void Of_ClearMissingProperties_ClearsThePropertiesTheFileLeavesOutButNotTheMatchKey()
    {
        // Act
        IReadOnlySet<long> clears = ImportClears.Of(Map, new AudienceImportOptions(false, ClearMissingProperties: true), MatchKey);

        // Assert
        clears.Should().BeEquivalentTo([Age]);
    }

    [Fact]
    public void Of_ClearBoth_ClearsEveryPropertyButTheMatchKey()
    {
        // Act
        IReadOnlySet<long> clears = ImportClears.Of(Map, new AudienceImportOptions(true, true), MatchKey);

        // Assert
        clears.Should().BeEquivalentTo([City, Region, Age]);
    }

    [Fact]
    public void Of_MatchKeyPropertyMappedToAColumn_IsNeverCleared()
    {
        // Arrange
        CsvColumnMap map = CsvColumnMapper.Build(new CsvColumnRequest(
            Properties, ["Identifier", "Mail"], "Identifier", new Dictionary<string, string> { ["email"] = "Mail" }));

        // Act
        IReadOnlySet<long> clears = ImportClears.Of(map, new AudienceImportOptions(true, true), MatchKey);

        // Assert
        clears.Should().NotContain(Email);
    }

    [Fact]
    public void Apply_StoredCellTheRowLeavesEmpty_IsWrittenEmpty()
    {
        // Act
        IReadOnlyDictionary<long, string> values = ImportClears.Apply(
            new Dictionary<long, string> { [City] = "Sofia" }, new HashSet<long> { City, Region, Age }, [Region, Age]);

        // Assert
        values.Should().BeEquivalentTo(new Dictionary<long, string> { [City] = "Sofia", [Region] = "", [Age] = "" });
    }

    [Fact]
    public void Apply_NoStoredCell_AddsNothingToClear()
    {
        // Act
        IReadOnlyDictionary<long, string> values = ImportClears.Apply(
            new Dictionary<long, string> { [City] = "Sofia" }, new HashSet<long> { City, Age }, [City]);

        // Assert
        values.Should().BeEquivalentTo(new Dictionary<long, string> { [City] = "Sofia" });
    }

    [Fact]
    public void Apply_StoredCellNotInTheClears_IsLeftOut()
    {
        // Act
        IReadOnlyDictionary<long, string> values = ImportClears.Apply(
            new Dictionary<long, string>(), new HashSet<long> { City }, [Age]);

        // Assert
        values.Should().BeEmpty();
    }
}

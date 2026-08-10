using Endatix.Core.Entities;
using Endatix.Core.UseCases.DataLists.Translations;

namespace Endatix.Core.Tests.UseCases.DataLists.Translations;

public class ReplaceDataListTranslationsCsvCommandTests
{
    [Fact]
    public void Ctor_ValidArgs_AssignsProperties()
    {
        const string csv = "value,default\r\napple,Apple\r\n";

        ReplaceDataListTranslationsCsvCommand command = new(42, csv, ["es", "fr"]);

        command.DataListId.Should().Be(42);
        command.Csv.Should().Be(csv);
        command.EnsureLocales.Should().Equal("es", "fr");
    }

    [Fact]
    public void Ctor_NullEnsureLocales_DefaultsToEmpty()
    {
        ReplaceDataListTranslationsCsvCommand command = new(1, "value,default\r\n");

        command.EnsureLocales.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ctor_NonPositiveDataListId_Throws(long dataListId)
    {
        Action act = () => _ = new ReplaceDataListTranslationsCsvCommand(dataListId, "value,default\r\n");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_NullCsv_Throws()
    {
        Action act = () => _ = new ReplaceDataListTranslationsCsvCommand(1, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MaxRows_MatchesDataListItemCap()
    {
        ReplaceDataListTranslationsCsvCommand.MAX_ROWS.Should().Be(DataList.MAX_ITEMS);
    }
}

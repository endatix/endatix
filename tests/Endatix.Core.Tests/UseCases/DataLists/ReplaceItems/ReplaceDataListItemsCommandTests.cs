using Endatix.Core.UseCases.DataLists.ReplaceItems;

namespace Endatix.Core.Tests.UseCases.DataLists.ReplaceItems;

public class ReplaceDataListItemsCommandTests
{
    [Fact]
    public void Ctor_ValidArgs_AssignsProperties()
    {
        ReplaceDataListItemInput item = new(Value: "apple", Label: "Apple");

        ReplaceDataListItemsCommand command = new(42, [item], ["es", "fr"]);

        command.DataListId.Should().Be(42);
        command.Items.Should().ContainSingle(i => i.Value == "apple" && i.Label == "Apple");
        command.EnsureLocales.Should().Equal("es", "fr");
    }

    [Fact]
    public void Ctor_NullEnsureLocales_DefaultsToEmpty()
    {
        ReplaceDataListItemsCommand command = new(1, []);

        command.EnsureLocales.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ctor_NonPositiveDataListId_Throws(long dataListId)
    {
        Action act = () => _ = new ReplaceDataListItemsCommand(dataListId, []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_NullItems_Throws()
    {
        Action act = () => _ = new ReplaceDataListItemsCommand(1, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ResolveLabels_PrefersLabelsMap()
    {
        ReplaceDataListItemInput item = new(
            Value: "apple",
            Labels: new Dictionary<string, string> { ["default"] = "Apple", ["es"] = "Manzana" },
            Label: "Ignored");

        item.ResolveLabels().Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["default"] = "Apple",
            ["es"] = "Manzana"
        });
    }

    [Fact]
    public void ResolveLabels_FallsBackToLegacyLabel()
    {
        ReplaceDataListItemInput item = new(Value: "apple", Label: "Apple");

        item.ResolveLabels().Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["default"] = "Apple"
        });
    }

    [Fact]
    public void ResolveLabels_MissingBoth_ReturnsNull()
    {
        ReplaceDataListItemInput item = new(Value: "apple");

        item.ResolveLabels().Should().BeNull();
    }
}

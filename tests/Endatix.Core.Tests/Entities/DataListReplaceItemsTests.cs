using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class DataListReplaceItemsTests
{
    [Fact]
    public void ReplaceItems_ValidItems_ReplacesCollection()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");
        dataList.AddItem("Apple", "apple");

        dataList.ReplaceItems(
        [
            (
                new Dictionary<string, string> { ["default"] = "Banana" },
                "banana"
            ),
            (
                new Dictionary<string, string> { ["default"] = "Cherry" },
                "cherry"
            )
        ]);

        dataList.Items.Select(i => i.Value).Should().Equal("banana", "cherry");
        dataList.Items.Select(i => i.DefaultLabel).Should().Equal("Banana", "Cherry");
    }

    [Fact]
    public void ReplaceItems_UnknownCulture_PreservesExistingItems()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");
        dataList.AddItem("Apple", "apple");

        Action act = () => dataList.ReplaceItems(
        [
            (
                new Dictionary<string, string> { ["default"] = "Ok" },
                "ok"
            ),
            (
                new Dictionary<string, string>
                {
                    ["default"] = "Apple",
                    ["fr"] = "Pomme"
                },
                "apple"
            )
        ]);

        act.Should().Throw<ArgumentException>();
        dataList.Items.Should().ContainSingle();
        dataList.Items.Single().Value.Should().Be("apple");
        dataList.Items.Single().DefaultLabel.Should().Be("Apple");
    }

    [Fact]
    public void ReplaceItems_InvalidItemValue_PreservesExistingItems()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");
        dataList.AddItem("Apple", "apple");

        Action act = () => dataList.ReplaceItems(
        [
            (
                new Dictionary<string, string> { ["default"] = "Banana" },
                "   "
            )
        ]);

        act.Should().Throw<ArgumentException>();
        dataList.Items.Should().ContainSingle(i => i.Value == "apple");
    }

    [Fact]
    public void ReplaceItems_EmptySequence_ClearsItems()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");
        dataList.AddItem("Apple", "apple");

        dataList.ReplaceItems([]);

        dataList.Items.Should().BeEmpty();
    }
}

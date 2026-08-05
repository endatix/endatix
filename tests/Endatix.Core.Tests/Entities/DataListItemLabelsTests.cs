using System.Reflection;
using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class DataListItemLabelsTests
{
    [Fact]
    public void Ctor_PersistsLabelsAsJson_IncludingDefault()
    {
        DataListItem item = new(
            new Dictionary<string, string>
            {
                ["default"] = "Apple",
                ["es"] = "Manzana"
            },
            "apple");

        item.DefaultLabel.Should().Be("Apple");
        item.LabelsJson.Should().Contain("\"default\"");
        item.LabelsJson.Should().Contain("Apple");
        item.LabelsJson.Should().Contain("\"es\"");
        item.Labels["es"].Should().Be("Manzana");
    }

    [Fact]
    public void RemoveTranslation_DoesNotTouchDefault()
    {
        DataListItem item = new(
            new Dictionary<string, string>
            {
                ["default"] = "Apple",
                ["es"] = "Manzana"
            },
            "apple");

        item.RemoveTranslation("es");

        item.Labels.Should().NotContainKey("es");
        item.Labels["default"].Should().Be("Apple");
        item.LabelsJson.Should().NotContain("Manzana");
    }

    [Fact]
    public void Ctor_WithoutDefault_Throws()
    {
        Action act = () => _ = new DataListItem(
            new Dictionary<string, string> { ["es"] = "Manzana" },
            "apple");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Labels_RebuildsCache_WhenBackingFieldJsonChanges()
    {
        DataListItem item = new("Apple", "apple");
        item.Labels["default"].Should().Be("Apple");

        typeof(DataListItem)
            .GetField("_labelsJson", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(item, """{"default":"Banana"}""");

        item.Labels["default"].Should().Be("Banana");
        item.DefaultLabel.Should().Be("Banana");
    }

    [Fact]
    public void NormalizeLabels_DefaultExceedingMaxLength_Throws()
    {
        string tooLong = new('x', DataListItem.MAX_LABEL_LENGTH + 1);

        Action act = () => DataListItem.NormalizeLabels(
            new Dictionary<string, string> { ["default"] = tooLong });

        act.Should().Throw<ArgumentException>()
            .WithParameterName("labels")
            .WithMessage($"*exceed {DataListItem.MAX_LABEL_LENGTH}*");
    }

    [Fact]
    public void Ctor_LocaleLabelExceedingMaxLength_Throws()
    {
        string tooLong = new('x', DataListItem.MAX_LABEL_LENGTH + 1);

        Action act = () => _ = new DataListItem(
            new Dictionary<string, string>
            {
                ["default"] = "Apple",
                ["es"] = tooLong
            },
            "apple");

        act.Should().Throw<ArgumentException>().WithParameterName("labels");
    }

    [Fact]
    public void Update_LabelExceedingMaxLength_Throws()
    {
        DataListItem item = new("Apple", "apple");
        string tooLong = new('x', DataListItem.MAX_LABEL_LENGTH + 1);

        Action act = () => item.Update(
            new Dictionary<string, string> { ["default"] = tooLong },
            "apple");

        act.Should().Throw<ArgumentException>().WithParameterName("labels");
        item.DefaultLabel.Should().Be("Apple");
    }

    [Fact]
    public void AddItem_LabelExceedingMaxLength_Throws()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");
        string tooLong = new('x', DataListItem.MAX_LABEL_LENGTH + 1);

        Action act = () => dataList.AddItem(tooLong, "apple");

        act.Should().Throw<ArgumentException>();
        dataList.Items.Should().BeEmpty();
    }
}

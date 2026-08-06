using System.Reflection;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.DataLists;

namespace Endatix.Core.Tests.UseCases.DataLists;

public class DataListDtoDefaultLabelProjectionTests
{
    [Fact]
    public void FromSearchItem_WhitespaceDefaultLabel_FallsBackToValue()
    {
        Dictionary<string, string> labels = new(StringComparer.Ordinal)
        {
            [DataListItem.DefaultLabelKey] = "   "
        };

        DataListItemDto dto = DataListDtoMapper.FromSearchItem(
            new DataListSearchItemResult(1, labels, "apple"));

        dto.Label.Should().Be("apple");
    }

    [Fact]
    public void FromSearchItem_MissingDefaultLabel_FallsBackToValue()
    {
        DataListItemDto dto = DataListDtoMapper.FromSearchItem(
            new DataListSearchItemResult(
                1,
                new Dictionary<string, string>(StringComparer.Ordinal),
                "apple"));

        dto.Label.Should().Be("apple");
    }

    [Fact]
    public void ToDataListDtoSpec_WhitespaceDefaultLabel_FallsBackToValue()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");
        DataListItem item = dataList.AddItem("Apple", "apple");
        SetLabelsJsonBackingField(item, """{"default":"   "}""");

        DataListDto dto = new DataListsSpecifications.ToDataListDtoSpec().Selector!.Compile()(dataList);

        DataListItemDto projected = dto.Items.Should().ContainSingle().Subject;
        projected.Label.Should().Be("apple");
        projected.Value.Should().Be("apple");
    }

    [Fact]
    public void ToDataListDtoSpec_NonEmptyDefaultLabel_UsesLabel()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities");
        dataList.AddItem("Apple", "apple");

        DataListDto dto = new DataListsSpecifications.ToDataListDtoSpec().Selector!.Compile()(dataList);

        dto.Items.Should().ContainSingle().Subject.Label.Should().Be("Apple");
    }

    private static void SetLabelsJsonBackingField(DataListItem item, string json) =>
        typeof(DataListItem)
            .GetField("_labelsJson", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(item, json);
}

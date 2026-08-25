using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;

namespace Endatix.Core.Tests.Specifications;

public class DataListsSpecificationsTests
{
    [Fact]
    public void ListSpec_CreatedToAtDateTimeMaxValue_IncludesRecordAtSentinel()
    {
        // Arrange: DateTime.MaxValue reaches this filter as the clamped
        // exclusive-day-end for CreatedTo=9999-12-31 (see
        // List.ParseExclusiveDayEndUtc, which clamps to it since
        // DateOnly.MaxValue has no "next day"). A record timestamped exactly
        // at the sentinel must not be dropped by a strict "<" comparison.
        var filter = new DataListsSpecifications.ListFilter(CreatedTo: DateTime.MaxValue);
        var spec = new DataListsSpecifications.ListSpec(filter);

        var atSentinel = CreateDataList(createdAt: DateTime.MaxValue);
        var wellBeforeSentinel = CreateDataList(createdAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // Act & Assert
        Matches(spec, atSentinel).Should().BeTrue();
        Matches(spec, wellBeforeSentinel).Should().BeTrue();
    }

    [Fact]
    public void ListSpec_ModifiedToAtDateTimeMaxValue_IncludesRecordAtSentinel()
    {
        var filter = new DataListsSpecifications.ListFilter(ModifiedTo: DateTime.MaxValue);
        var spec = new DataListsSpecifications.ListSpec(filter);

        var atSentinel = CreateDataList(modifiedAt: DateTime.MaxValue);

        Matches(spec, atSentinel).Should().BeTrue();
    }

    [Fact]
    public void ListSpec_CreatedToNonSentinelValue_StaysExclusive()
    {
        // Arrange: ordinary (non-sentinel) CreatedTo bounds must remain
        // exclusive -- only the DateTime.MaxValue sentinel gets inclusive
        // treatment.
        var bound = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var filter = new DataListsSpecifications.ListFilter(CreatedTo: bound);
        var spec = new DataListsSpecifications.ListSpec(filter);

        var atBound = CreateDataList(createdAt: bound);
        var beforeBound = CreateDataList(createdAt: bound.AddSeconds(-1));

        // Act & Assert
        Matches(spec, atBound).Should().BeFalse();
        Matches(spec, beforeBound).Should().BeTrue();
    }

    private static bool Matches(ISpecification<DataList> spec, DataList dataList)
    {
        return spec.WhereExpressions.All(where => where.FilterFunc(dataList));
    }

    private static DataList CreateDataList(DateTime? createdAt = null, DateTime? modifiedAt = null)
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities", null, "CITIES");

        if (createdAt.HasValue)
        {
            typeof(DataList)
                .GetProperty(nameof(DataList.CreatedAt))!
                .SetValue(dataList, createdAt.Value);
        }

        if (modifiedAt.HasValue)
        {
            typeof(DataList)
                .GetProperty(nameof(DataList.ModifiedAt))!
                .SetValue(dataList, modifiedAt.Value);
        }

        return dataList;
    }
}

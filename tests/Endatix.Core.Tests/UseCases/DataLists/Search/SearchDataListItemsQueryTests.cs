using Endatix.Core.Common.Translations;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.UseCases.DataLists.Search;

namespace Endatix.Core.Tests.UseCases.DataLists.Search;

public class SearchDataListItemsQueryTests
{
    [Fact]
    public void Ctor_ValidArgs_AssignsNormalizedProperties()
    {
        // Arrange & Act
        SearchDataListItemsQuery query = new(
            dataListId: 42,
            query: "  york  ",
            skip: 10,
            take: 25,
            matchMode: DataListSearchMatchMode.StartsWith,
            locale: "es",
            includeLocales: ["fr", "not-a-culture"],
            requireActive: false);

        // Assert
        query.DataListId.Should().Be(42);
        query.Query.Should().Be("york");
        query.Skip.Should().Be(10);
        query.Take.Should().Be(25);
        query.MatchMode.Should().Be(DataListSearchMatchMode.StartsWith);
        query.Locale.Should().Be(CultureCode.Parse("es"));
        query.IncludeLocales.Should().ContainSingle(x => x == CultureCode.Parse("fr"));
        query.RequireActive.Should().BeFalse();
    }

    [Fact]
    public void Ctor_Defaults_RequireActiveTrueAndContainsMatch()
    {
        // Arrange & Act
        SearchDataListItemsQuery query = new(1, null, 0, 10);

        // Assert
        query.RequireActive.Should().BeTrue();
        query.MatchMode.Should().Be(DataListSearchMatchMode.Contains);
        query.Locale.Should().BeNull();
        query.IncludeLocales.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_TakeAboveMax_ClampsToMaxTake()
    {
        // Arrange & Act
        SearchDataListItemsQuery query = new(1, null, 0, SearchDataListItemsQuery.MaxTake + 50);

        // Assert
        query.Take.Should().Be(SearchDataListItemsQuery.MaxTake);
        query.Take.Should().Be(PagedRequestLimits.MAX_PAGE_SIZE);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ctor_NonPositiveDataListId_Throws(long dataListId)
    {
        // Act
        Action act = () => _ = new SearchDataListItemsQuery(dataListId, null, 0, 10);

        // Assert
        act.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("dataListId");
    }
}

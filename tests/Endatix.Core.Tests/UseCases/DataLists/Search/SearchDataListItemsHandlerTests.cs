using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists.Search;
using Endatix.Core.Common.Translations;
using Endatix.Core.Exceptions;
using NSubstitute.ExceptionExtensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Core.Tests.UseCases.DataLists.Search;

public class SearchDataListItemsHandlerTests
{
    private readonly IDataListRepository _repository;
    private readonly SearchDataListItemsHandler _sut;

    public SearchDataListItemsHandlerTests()
    {
        _repository = Substitute.For<IDataListRepository>();
        _sut = new SearchDataListItemsHandler(_repository, NullLogger<SearchDataListItemsHandler>.Instance);
    }

    private static DataListSearchItemResult Item(
        long id,
        string value,
        string defaultLabel,
        params (string Locale, string Text)[] extraLabels)
    {
        Dictionary<string, string> labels = new(StringComparer.Ordinal)
        {
            [DataListItem.DefaultLabelKey] = defaultLabel
        };

        foreach ((string locale, string text) in extraLabels)
        {
            labels[locale] = text;
        }

        return new DataListSearchItemResult(id, labels, value);
    }

    private void SetupSearch(DataListSearchPageResult page) =>
        _repository.SearchItemsAsync(
                Arg.Any<DataListSearchCriteria>(),
                Arg.Any<CancellationToken>())
            .Returns(page);

    [Fact]
    public async Task Handle_DataListNotFound_ReturnsNotFound()
    {
        _repository.SearchItemsAsync(
                Arg.Any<DataListSearchCriteria>(),
                Arg.Any<CancellationToken>())
            .Returns((DataListSearchPageResult?)null);

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(1, null, 0, 10),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsPagedItems()
    {
        SetupSearch(new DataListSearchPageResult(
            1,
            2,
            [
                Item(1, "NYC", "New York"),
                Item(2, "LA", "Los Angeles")
            ]));

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(1, null, 0, 10),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalRecords.Should().Be(2);
        result.Value.Items.First().Label.Should().Be("New York");
    }

    [Fact]
    public async Task Handle_PreservesFullLabels_AndResolvesDefaultLabel()
    {
        SetupSearch(new DataListSearchPageResult(
            1,
            1,
            [Item(1, "apple", "Apple", ("es", "Manzana"))]));

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(1, null, 0, 10),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        var item = result.Value!.Items.Single();
        item.Label.Should().Be("Apple");
        item.Labels.Should().ContainKey("es").WhoseValue.Should().Be("Manzana");
        item.Labels[DataListItem.DefaultLabelKey].Should().Be("Apple");
    }

    [Fact]
    public async Task Handle_WithQuery_FiltersItems()
    {
        SetupSearch(new DataListSearchPageResult(
            1,
            1,
            [Item(1, "NYC", "New York")]));

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(1, "New", 0, 10),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.TotalRecords.Should().Be(1);

        await _repository.Received(1).SearchItemsAsync(
            Arg.Is<DataListSearchCriteria>(c =>
                c.DataListId == 1
                && c.Query == "New"
                && c.Skip == 0
                && c.Take == 10
                && c.MatchMode == DataListSearchMatchMode.Contains
                && c.Locale == null
                && c.IncludeLocales.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithLocale_PassesLocaleToRepository()
    {
        SetupSearch(new DataListSearchPageResult(
            1,
            1,
            [Item(1, "apple", "Apple", ("es", "Manzana"))]));

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(
                1,
                "Manz",
                0,
                10,
                new SearchDataListItemsOptions(DataListSearchMatchMode.StartsWith, Locale: "es")),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        await _repository.Received(1).SearchItemsAsync(
            Arg.Is<DataListSearchCriteria>(c =>
                c.DataListId == 1
                && c.Query == "Manz"
                && c.Skip == 0
                && c.Take == 10
                && c.MatchMode == DataListSearchMatchMode.StartsWith
                && c.Locale == CultureCode.Parse("es")
                && c.IncludeLocales.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithIncludeLocales_PassesIncludeLocalesToRepository()
    {
        SetupSearch(new DataListSearchPageResult(
            1,
            1,
            [Item(1, "apple", "Apple", ("es", "Manzana"), ("fr", "Pomme"))]));

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(
                1,
                null,
                0,
                10,
                new SearchDataListItemsOptions(IncludeLocales: ["es", "fr"])),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        await _repository.Received(1).SearchItemsAsync(
            Arg.Is<DataListSearchCriteria>(c =>
                c.DataListId == 1
                && c.IncludeLocales.Count == 2
                && c.IncludeLocales[0] == CultureCode.Parse("es")
                && c.IncludeLocales[1] == CultureCode.Parse("fr")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Paging_CorrectlyCalculatesTotal()
    {
        SetupSearch(new DataListSearchPageResult(1, 100, []));

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(1, null, 50, 10),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.TotalRecords.Should().Be(100);
        result.Value.Page.Should().Be(6);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalPages.Should().Be(10);
    }

    [Theory]
    [InlineData(26, 25, 2)]
    [InlineData(51, 25, 3)]
    [InlineData(1, 25, 1)]
    public async Task Handle_NonAlignedOffset_ReturnsCorrectPage(int skip, int take, int expectedPage)
    {
        SetupSearch(new DataListSearchPageResult(1, 100, []));

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(1, null, skip, take),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Page.Should().Be(expectedPage);
    }

    [Theory]
    [InlineData(100, 10)]
    [InlineData(200, 10)]
    public async Task Handle_OutOfRangeSkip_ReturnsLastPage(int skip, int take)
    {
        SetupSearch(new DataListSearchPageResult(1, 100, []));

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(1, null, skip, take),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Page.Should().Be(10);
    }

    [Fact]
    public async Task Handle_ZeroTotalRecords_ReturnsEmptyPaged()
    {
        SetupSearch(new DataListSearchPageResult(1, 0, []));

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(1, null, 0, 10),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Page.Should().Be(1);
        result.Value.TotalPages.Should().Be(0);
        result.Value.TotalRecords.Should().Be(0);
    }

    [Theory]
    [InlineData(-1, 10, "skip")]
    [InlineData(0, 0, "take")]
    [InlineData(-5, 5, "skip")]
    public void QueryCtor_WithInvalidPaging_ThrowsArgumentException(int skip, int take, string expectedParam)
    {
        Action act = () => _ = new SearchDataListItemsQuery(1, null, skip, take);

        act.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be(expectedParam);
    }

    [Fact]
    public void QueryCtor_WithInvalidLocale_ThrowsArgumentException()
    {
        Action act = () => _ = new SearchDataListItemsQuery(
            1,
            "App",
            0,
            10,
            new SearchDataListItemsOptions(Locale: "not a culture!"));

        act.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("cultureCode");
    }

    [Fact]
    public void QueryCtor_WithLocaleTooLong_ThrowsArgumentException()
    {
        string locale = new('a', IHasTranslations.MAX_CULTURE_CODE_LENGTH + 1);

        Action act = () => _ = new SearchDataListItemsQuery(
            1,
            null,
            0,
            10,
            new SearchDataListItemsOptions(Locale: locale));

        act.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("cultureCode");
    }

    /// <summary>
    /// BCL and EF Core argument text can name columns, expressions and providers, so it must never reach
    /// the response - only author-written text does.
    /// </summary>
    [Fact]
    public async Task Handle_WhenSearchRejectsAnArgument_DoesNotEchoTheExceptionMessage()
    {
        // Arrange
        const string internalDetail = "The LINQ expression 'DbSet<DataListItem>.Where(...)' could not be translated";
        _repository.SearchItemsAsync(Arg.Any<DataListSearchCriteria>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ArgumentException(internalDetail, "cultureCode"));

        // Act
        var result = await _sut.Handle(new SearchDataListItemsQuery(1, "App", 0, 10), CancellationToken.None);

        // Assert
        var error = result.ValidationErrors.Should().ContainSingle().Which;
        error.ErrorMessage.Should().NotContain("LINQ");
        error.Identifier.Should().Be(nameof(SearchDataListItemsQuery.Locale));
        error.ErrorMessage.Should().Be("Invalid locale.");
    }

    /// <summary>
    /// A domain rule that opted into <see cref="IEndUserSafeError"/> is surfaced intact.
    /// </summary>
    [Fact]
    public async Task Handle_WhenSearchRejectsWithASafeDomainError_SurfacesTheDomainMessage()
    {
        // Arrange
        _repository.SearchItemsAsync(Arg.Any<DataListSearchCriteria>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainValidationException(
                "'zz-ZZ' is not in the data list AvailableLocales catalog.",
                "cultureCode"));

        // Act
        var result = await _sut.Handle(new SearchDataListItemsQuery(1, "App", 0, 10), CancellationToken.None);

        // Assert
        var error = result.ValidationErrors.Should().ContainSingle().Which;
        error.Identifier.Should().Be(nameof(SearchDataListItemsQuery.Locale));
        error.ErrorMessage.Should().Be("'zz-ZZ' is not in the data list AvailableLocales catalog.");
        error.ErrorMessage.Should().NotContain("cultureCode");
    }
}

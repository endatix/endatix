using Endatix.Core.Common.Translations;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.List;

namespace Endatix.Core.Tests.UseCases.DataLists.List;

public class ListDataListsHandlerTests
{
    private readonly IRepository<DataList> _repository;
    private readonly ListDataListsHandler _sut;

    public ListDataListsHandlerTests()
    {
        _repository = Substitute.For<IRepository<DataList>>();
        _sut = new ListDataListsHandler(_repository);
    }

    [Fact]
    public async Task Handle_NoRecords_ReturnsEmptyPageWithoutListing()
    {
        // Arrange
        _repository.CountAsync(Arg.Any<DataListsSpecifications.ListSpec>(), Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        var result = await _sut.Handle(new ListDataListsQuery(1, 10), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.TotalRecords.Should().Be(0);
        result.Value.Items.Should().BeEmpty();

        await _repository.DidNotReceive().ListAsync(
            Arg.Any<DataListsSpecifications.ListWithPagingToDtoSpec>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithRecords_ReturnsPagedDtos()
    {
        // Arrange
        List<DataListDto> page =
        [
            new(1, "Cities", null, DateTime.UtcNow, null, true, 2, "en", ["es"], Array.Empty<DataListItemDto>()),
            new(2, "Countries", "All", DateTime.UtcNow, null, true, 0, "en", [], Array.Empty<DataListItemDto>())
        ];

        _repository.CountAsync(Arg.Any<DataListsSpecifications.ListSpec>(), Arg.Any<CancellationToken>())
            .Returns(2);
        _repository.ListAsync(Arg.Any<DataListsSpecifications.ListWithPagingToDtoSpec>(), Arg.Any<CancellationToken>())
            .Returns(page);

        // Act
        var result = await _sut.Handle(new ListDataListsQuery(1, 10), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.TotalRecords.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Select(x => x.Name).Should().Equal("Cities", "Countries");

        await _repository.Received(1).CountAsync(
            Arg.Any<DataListsSpecifications.ListSpec>(),
            Arg.Any<CancellationToken>());
        await _repository.Received(1).ListAsync(
            Arg.Any<DataListsSpecifications.ListWithPagingToDtoSpec>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithHasLocale_PassesLocaleToSpecs()
    {
        // Arrange
        _repository.CountAsync(Arg.Any<DataListsSpecifications.ListSpec>(), Arg.Any<CancellationToken>())
            .Returns(1);
        _repository.ListAsync(Arg.Any<DataListsSpecifications.ListWithPagingToDtoSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<DataListDto>
            {
                new(5, "Cities", null, DateTime.UtcNow, null, true, 1, "en", ["es"], Array.Empty<DataListItemDto>())
            });

        // Act
        await _sut.Handle(new ListDataListsQuery(1, 10, "es"), TestContext.Current.CancellationToken);

        // Assert
        await _repository.Received(1).CountAsync(
            Arg.Is<DataListsSpecifications.ListSpec>(spec => SpecFiltersByLocale(spec, "es")),
            Arg.Any<CancellationToken>());
        await _repository.Received(1).ListAsync(
            Arg.Is<DataListsSpecifications.ListWithPagingToDtoSpec>(spec => SpecFiltersByLocale(spec, "es")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithQuery_PassesSearchToSpecs()
    {
        // Arrange
        _repository.CountAsync(Arg.Any<DataListsSpecifications.ListSpec>(), Arg.Any<CancellationToken>())
            .Returns(1);
        _repository.ListAsync(Arg.Any<DataListsSpecifications.ListWithPagingToDtoSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<DataListDto>
            {
                new(5, "Cities", "Major cities", DateTime.UtcNow, null, true, 1, "en", [], Array.Empty<DataListItemDto>())
            });

        // Act
        await _sut.Handle(new ListDataListsQuery(1, 10, Query: "major"), TestContext.Current.CancellationToken);

        // Assert
        await _repository.Received(1).CountAsync(
            Arg.Is<DataListsSpecifications.ListSpec>(spec => SpecFiltersByQuery(spec, "major")),
            Arg.Any<CancellationToken>());
        await _repository.Received(1).ListAsync(
            Arg.Is<DataListsSpecifications.ListWithPagingToDtoSpec>(spec => SpecFiltersByQuery(spec, "major")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SecondPage_ComputesSkipFromPaging()
    {
        // Arrange
        _repository.CountAsync(Arg.Any<DataListsSpecifications.ListSpec>(), Arg.Any<CancellationToken>())
            .Returns(25);
        _repository.ListAsync(Arg.Any<DataListsSpecifications.ListWithPagingToDtoSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<DataListDto>
            {
                new(99, "Page2", null, DateTime.UtcNow, null, true, 0, "en", [], Array.Empty<DataListItemDto>())
            });

        // Act
        var result = await _sut.Handle(new ListDataListsQuery(2, 10), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalRecords.Should().Be(25);
        result.Value.Items.Should().ContainSingle(x => x.Id == 99);
    }

    private static bool SpecFiltersByLocale(ISpecification<DataList> spec, string locale)
    {
        DataList withLocale = new(SampleData.TENANT_ID, "WithLocale");
        withLocale.AddCulture(CultureCode.Parse(locale));

        DataList withoutLocale = new(SampleData.TENANT_ID, "WithoutLocale");
        withoutLocale.AddCulture(CultureCode.Parse("fr"));

        bool Matches(DataList dataList) =>
            spec.WhereExpressions.Any()
            && spec.WhereExpressions.All(expression => expression.FilterFunc(dataList));

        return Matches(withLocale) && !Matches(withoutLocale);
    }

    private static bool SpecFiltersByQuery(ISpecification<DataList> spec, string query)
    {
        DataList matching = new(SampleData.TENANT_ID, "Cities", "Major cities");
        DataList other = new(SampleData.TENANT_ID, "Countries", "ISO codes");

        bool Matches(DataList dataList) =>
            spec.WhereExpressions.Any()
            && spec.WhereExpressions.All(expression => expression.FilterFunc(dataList));

        return Matches(matching) && !Matches(other);
    }
}

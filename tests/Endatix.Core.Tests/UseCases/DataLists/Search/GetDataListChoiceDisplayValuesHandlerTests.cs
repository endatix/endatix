using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.DataLists.Search;

namespace Endatix.Core.Tests.UseCases.DataLists.Search;

public class GetDataListChoiceDisplayValuesHandlerTests
{
    private readonly IRepository<DataList> _repository;
    private readonly GetDataListChoiceDisplayValuesHandler _sut;

    public GetDataListChoiceDisplayValuesHandlerTests()
    {
        _repository = Substitute.For<IRepository<DataList>>();
        _sut = new GetDataListChoiceDisplayValuesHandler(_repository);
    }

    private DataList GivenDataList()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Fruit", defaultLocale: "en") { Id = 1 };
        dataList.AddCulture(CultureCode.Parse("es"));
        dataList.AddCulture(CultureCode.Parse("fr"));
        dataList.ReplaceItems(
        [
            (new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["default"] = "Apple",
                ["es"] = "Manzana",
                ["fr"] = "Pomme"
            }, "apple")
        ]);

        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsByValuesSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(dataList);

        return dataList;
    }

    [Fact]
    public async Task Handle_DataListNotFound_ReturnsNotFound()
    {
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsByValuesSpec>(),
                Arg.Any<CancellationToken>())
            .Returns((DataList?)null);

        var result = await _sut.Handle(
            new GetDataListChoiceDisplayValuesQuery(1, ["apple"]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_WithoutIncludeLocales_ProjectsDefaultOnly()
    {
        GivenDataList();

        var result = await _sut.Handle(
            new GetDataListChoiceDisplayValuesQuery(1, ["apple"]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        DataListChoiceDisplayValueDto item = result.Value!.Single();
        item.Labels.Should().BeEquivalentTo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["default"] = "Apple"
        });
    }

    [Fact]
    public async Task Handle_WithIncludeLocales_ProjectsRequestedKeys()
    {
        GivenDataList();

        var result = await _sut.Handle(
            new GetDataListChoiceDisplayValuesQuery(1, ["apple"], includeLocales: ["es"]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        DataListChoiceDisplayValueDto item = result.Value!.Single();
        item.Labels.Should().BeEquivalentTo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["default"] = "Apple",
            ["es"] = "Manzana"
        });
        item.Labels.Should().NotContainKey("fr");
    }

    [Fact]
    public async Task Handle_WithUnknownIncludeLocales_IgnoresThem()
    {
        GivenDataList();

        var result = await _sut.Handle(
            new GetDataListChoiceDisplayValuesQuery(1, ["apple"], includeLocales: ["de", "es"]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.Single().Labels.Keys.Should().BeEquivalentTo(["default", "es"]);
    }
}

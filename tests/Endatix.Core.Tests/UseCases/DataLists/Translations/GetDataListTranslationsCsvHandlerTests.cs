using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.DataLists.Translations;

namespace Endatix.Core.Tests.UseCases.DataLists.Translations;

public class GetDataListTranslationsCsvHandlerTests
{
    private readonly IRepository<DataList> _repository;
    private readonly GetDataListTranslationsCsvHandler _sut;

    public GetDataListTranslationsCsvHandlerTests()
    {
        _repository = Substitute.For<IRepository<DataList>>();
        _sut = new GetDataListTranslationsCsvHandler(_repository);
    }

    [Fact]
    public async Task Handle_DataListNotFound_ReturnsNotFound()
    {
        // Arrange
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(),
                Arg.Any<CancellationToken>())
            .Returns((DataList?)null);

        // Act
        var result = await _sut.Handle(
            new GetDataListTranslationsCsvQuery(1),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_ListWithLocales_WritesDefaultFirstAndRowsByValue()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities", normalizedName: "cities") { Id = 1 };
        dataList.AddCulture(CultureCode.Parse("es"));
        dataList.AddItem(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [SurveyJsTranslationKeys.DefaultKey] = "Banana",
                ["es"] = "Plátano"
            },
            "banana");
        dataList.AddItem("Apple", "apple");
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(dataList);

        // Act
        var result = await _sut.Handle(
            new GetDataListTranslationsCsvQuery(1),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Csv.Should().Be(
            "value,default,es\r\n" +
            "apple,Apple,\r\n" +
            "banana,Banana,Plátano\r\n");
    }

    [Fact]
    public async Task Handle_ValidRequest_SuggestsSlugFileName()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "US Cities", normalizedName: "US Cities") { Id = 7 };
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(dataList);

        // Act
        var result = await _sut.Handle(
            new GetDataListTranslationsCsvQuery(7),
            TestContext.Current.CancellationToken);

        // Assert
        result.Value.FileName.Should().Be("us-cities-translations.csv");
    }
}

using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.DataLists.GetById;

namespace Endatix.Core.Tests.UseCases.DataLists.GetById;

public class GetDataListByIdHandlerTests
{
    private readonly IRepository<DataList> _repository;
    private readonly GetDataListByIdHandler _sut;

    public GetDataListByIdHandlerTests()
    {
        _repository = Substitute.For<IRepository<DataList>>();
        _sut = new GetDataListByIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_DataListNotFound_ReturnsNotFound()
    {
        // Arrange
        _repository.FirstOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns((DataList?)null);

        // Act
        var result = await _sut.Handle(new GetDataListByIdQuery(42), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Data list not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsMappedDtoWithItemsAndLocales()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities", "Major cities") { Id = 7 };
        dataList.AddCulture("es");
        DataListItem nyc = dataList.AddItem(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [SurveyJsTranslationKeys.DefaultKey] = "New York",
                ["es"] = "Nueva York"
            },
            "NYC");
        nyc.Id = 101;
        DataListItem la = dataList.AddItem("Los Angeles", "LA");
        la.Id = 102;

        _repository.FirstOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(dataList);

        // Act
        var result = await _sut.Handle(new GetDataListByIdQuery(7), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(7);
        result.Value.Name.Should().Be("Cities");
        result.Value.Description.Should().Be("Major cities");
        result.Value.IsActive.Should().BeTrue();
        result.Value.DefaultLocale.Should().Be(SurveyJsTranslationKeys.FallbackDefaultCulture);
        result.Value.AvailableLocales.Should().Equal("es");
        result.Value.ItemsCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);

        var nycDto = result.Value.Items.Should().ContainSingle(i => i.Value == "NYC").Subject;
        nycDto.Id.Should().Be(101);
        nycDto.Label.Should().Be("New York");
        nycDto.Labels.Should().ContainKey(SurveyJsTranslationKeys.DefaultKey).WhoseValue.Should().Be("New York");
        nycDto.Labels.Should().ContainKey("es").WhoseValue.Should().Be("Nueva York");

        await _repository.Received(1).FirstOrDefaultAsync(
            Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequestWithoutItems_ReturnsEmptyItemsCollection()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Empty list") { Id = 3 };
        _repository.FirstOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        // Act
        var result = await _sut.Handle(new GetDataListByIdQuery(3), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.ItemsCount.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
        result.Value.AvailableLocales.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveDataListId_ThrowsArgumentException(long dataListId)
    {
        // Act
        Action act = () => _ = new GetDataListByIdQuery(dataListId);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}

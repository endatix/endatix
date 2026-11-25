using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.DataLists.Locales;
using MediatR;

namespace Endatix.Core.Tests.UseCases.DataLists.Locales;

public class SetDataListDefaultLocaleHandlerTests
{
    private readonly IRepository<DataList> _repository;
    private readonly IMediator _mediator;
    private readonly SetDataListDefaultLocaleHandler _sut;

    public SetDataListDefaultLocaleHandlerTests()
    {
        _repository = Substitute.For<IRepository<DataList>>();
        _mediator = Substitute.For<IMediator>();
        _sut = new SetDataListDefaultLocaleHandler(_repository, _mediator);
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
            new SetDataListDefaultLocaleCommand(1, "en"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_SetsDefaultLocaleAndReturnsDto()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities") { Id = 1 };
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(dataList);

        // Act
        var result = await _sut.Handle(
            new SetDataListDefaultLocaleCommand(1, "FR"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.DefaultLocale.Should().Be("fr");
        dataList.DefaultLocale.Should().Be("fr");
        dataList.DefaultCulture.Should().Be("fr");

        await _repository.Received(1).UpdateAsync(dataList, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_PublishesLocalesUpdatedEvent()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities") { Id = 1 };
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(dataList);

        // Act
        await _sut.Handle(
            new SetDataListDefaultLocaleCommand(1, "de"),
            TestContext.Current.CancellationToken);

        // Assert
        await _mediator.Received(1).Publish(
            Arg.Is<DataListUpdatedEvent>(e =>
                e.DataList.Id == 1
                && e.Reason == DataListUpdateReasons.LocalesUpdated),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SyntheticDefaultKey_ReturnsInvalid()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities") { Id = 1 };
        string originalDefault = dataList.DefaultLocale;
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(dataList);

        // Act
        var result = await _sut.Handle(
            new SetDataListDefaultLocaleCommand(1, "default"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.Identifier == "DefaultLocale");
        dataList.DefaultLocale.Should().Be(originalDefault);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidCultureCode_ReturnsInvalid()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities") { Id = 1 };
        string originalDefault = dataList.DefaultLocale;
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(dataList);

        // Act
        var result = await _sut.Handle(
            new SetDataListDefaultLocaleCommand(1, "not a culture!"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.Identifier == "DefaultLocale");
        dataList.DefaultLocale.Should().Be(originalDefault);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>());
    }
}

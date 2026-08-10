using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.DataLists.Locales;
using MediatR;

namespace Endatix.Core.Tests.UseCases.DataLists.Locales;

public class RemoveDataListLocaleHandlerTests
{
    private readonly IRepository<DataList> _repository;
    private readonly IMediator _mediator;
    private readonly RemoveDataListLocaleHandler _sut;

    public RemoveDataListLocaleHandlerTests()
    {
        _repository = Substitute.For<IRepository<DataList>>();
        _mediator = Substitute.For<IMediator>();
        _sut = new RemoveDataListLocaleHandler(_repository, _mediator);
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
            new RemoveDataListLocaleCommand(1, "es"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_RemovesLocaleAndStripsItemLabels()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities") { Id = 1 };
        dataList.AddCulture(CultureCode.Parse("es"));
        dataList.AddItem(
            new Dictionary<string, string>
            {
                ["default"] = "Apple",
                ["es"] = "Manzana"
            },
            "apple");
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(dataList);

        // Act
        var result = await _sut.Handle(
            new RemoveDataListLocaleCommand(1, "ES"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.AvailableLocales.Should().BeEmpty();
        dataList.AvailableLocales.Should().BeEmpty();
        dataList.Items.Single().Labels.Should().NotContainKey("es");
        dataList.Items.Single().Labels["default"].Should().Be("Apple");

        await _repository.Received(1).UpdateAsync(dataList, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_PublishesLocalesUpdatedEvent()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities") { Id = 1 };
        dataList.AddCulture(CultureCode.Parse("es"));
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(dataList);

        // Act
        await _sut.Handle(
            new RemoveDataListLocaleCommand(1, "es"),
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
        dataList.AddCulture(CultureCode.Parse("es"));
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(dataList);

        // Act
        var result = await _sut.Handle(
            new RemoveDataListLocaleCommand(1, "default"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.Identifier == "Locale");
        dataList.AvailableLocales.Should().Equal("es");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidCultureCode_ReturnsInvalid()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities") { Id = 1 };
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(dataList);

        // Act
        var result = await _sut.Handle(
            new RemoveDataListLocaleCommand(1, "not a culture!"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.Identifier == "Locale");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MissingLocale_IsIdempotent()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities") { Id = 1 };
        dataList.AddCulture(CultureCode.Parse("fr"));
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(dataList);

        // Act
        var result = await _sut.Handle(
            new RemoveDataListLocaleCommand(1, "es"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.AvailableLocales.Should().Equal("fr");
        await _repository.Received(1).UpdateAsync(dataList, Arg.Any<CancellationToken>());
    }
}

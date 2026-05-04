using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.DataLists.ReplaceItems;
using MediatR;

namespace Endatix.Core.Tests.UseCases.DataLists.ReplaceItems;

public class ReplaceDataListItemsHandlerTests
{
    private readonly IRepository<DataList> _repository;
    private readonly IMediator _mediator;
    private readonly ReplaceDataListItemsHandler _sut;

    public ReplaceDataListItemsHandlerTests()
    {
        _repository = Substitute.For<IRepository<DataList>>();
        _mediator = Substitute.For<IMediator>();
        _sut = new ReplaceDataListItemsHandler(_repository, _mediator);
    }

    [Fact]
    public async Task Handle_DataListNotFound_ReturnsNotFound()
    {
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns((DataList?)null);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, [new("City", "NYC")]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_ValidRequest_TrimsLabelsAndValues()
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities") { Id = 1 };
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, [new("  New York  ", "  NYC  ")]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().Contain(i => i.Label == "New York" && i.Value == "NYC");
    }

    [Fact]
    public async Task Handle_ValidRequest_ReplacesItemsAndReturnsDto()
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities") { Id = 1 };
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, [new("New York", "NYC"), new("Los Angeles", "LA")]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(2);

        await _repository.Received(1).UpdateAsync(dataList, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_PublishesDataListUpdatedEvent()
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities") { Id = 1 };
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        await _sut.Handle(
            new ReplaceDataListItemsCommand(1, [new("New York", "NYC")]),
            TestContext.Current.CancellationToken);

        await _mediator.Received(1).Publish(
            Arg.Is<DataListUpdatedEvent>(e =>
                e.DataList.Id == 1 &&
                e.Reason == DataListUpdateReasons.ItemsReplaced
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_EmptyLabel_ReturnsInvalid()
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities") { Id = 1 };
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, [new("", "NYC")]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.Identifier == "Items[0].Label");
    }

    [Fact]
    public async Task Handle_WhitespaceLabel_ReturnsInvalid()
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities") { Id = 1 };
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, [new("   ", "NYC")]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public async Task Handle_EmptyValue_ReturnsInvalid()
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities") { Id = 1 };
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, [new("City", "")]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.Identifier == "Items[0].Value");
    }

    [Fact]
    public async Task Handle_WhitespaceValue_ReturnsInvalid()
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities") { Id = 1 };
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, [new("City", "   ")]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void Handle_CommandWithNullItems_ThrowsArgumentNullException()
    {
        Action act = () => _ = new ReplaceDataListItemsCommand(1, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Handle_CommandWithZeroId_ThrowsArgumentException()
    {
        Action act = () => _ = new ReplaceDataListItemsCommand(0, [new("City", "NYC")]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Handle_CommandWithNegativeId_ThrowsArgumentException()
    {
        Action act = () => _ = new ReplaceDataListItemsCommand(-1, [new("City", "NYC")]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Handle_EmptyItemsList_ReplacesWithEmptyCollection()
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities") { Id = 1 };
        dataList.ReplaceItems([("LA", "Los Angeles")]);
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, []),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.Items.Should().BeEmpty();
    }
}
using Endatix.Core.Common.Translations;
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

    private static ReplaceDataListItemInput Item(string label, string value) =>
        new(Value: value, Label: label);

    [Fact]
    public async Task Handle_DataListNotFound_ReturnsNotFound()
    {
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns((DataList?)null);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, [Item("City", "NYC")]),
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
            new ReplaceDataListItemsCommand(1, [Item("  New York  ", "  NYC  ")]),
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
            new ReplaceDataListItemsCommand(1, [Item("New York", "NYC"), Item("Los Angeles", "LA")]),
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
            new ReplaceDataListItemsCommand(1, [Item("New York", "NYC")]),
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
            new ReplaceDataListItemsCommand(1, [Item("", "NYC")]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.Identifier == "Items[0].Labels");
    }

    [Fact]
    public async Task Handle_WhitespaceLabel_ReturnsInvalid()
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities") { Id = 1 };
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, [Item("   ", "NYC")]),
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
            new ReplaceDataListItemsCommand(1, [Item("City", "")]),
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
            new ReplaceDataListItemsCommand(1, [Item("City", "   ")]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public async Task Handle_UnknownLocaleInLabels_ReturnsInvalid()
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities") { Id = 1 };
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, [
                new(
                    Value: "NYC",
                    Labels: new Dictionary<string, string>
                    {
                        ["default"] = "New York",
                        ["es"] = "Nueva York"
                    })
            ]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.Identifier == "Items[0].Labels.es");
    }

    [Fact]
    public async Task Handle_LabelsWithCatalogLocale_Succeeds()
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities") { Id = 1 };
        dataList.AddCulture(CultureCode.Parse("es"));
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, [
                new(
                    Value: "NYC",
                    Labels: new Dictionary<string, string>
                    {
                        ["default"] = "New York",
                        ["es"] = "Nueva York"
                    })
            ]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.Items.Single().Labels["es"].Should().Be("Nueva York");
    }

    [Fact]
    public async Task Handle_EnsureLocales_AddsCultureThenReplaces()
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities") { Id = 1 };
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(
                1,
                [
                    new(
                        Value: "NYC",
                        Labels: new Dictionary<string, string>
                        {
                            ["default"] = "New York",
                            ["es"] = "Nueva York"
                        })
                ],
                ensureLocales: ["es"]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        dataList.AvailableLocales.Should().Contain("es");
        result.Value!.Items.Single().Labels["es"].Should().Be("Nueva York");
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
        Action act = () => _ = new ReplaceDataListItemsCommand(0, [Item("City", "NYC")]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Handle_CommandWithNegativeId_ThrowsArgumentException()
    {
        Action act = () => _ = new ReplaceDataListItemsCommand(-1, [Item("City", "NYC")]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Handle_EmptyItemsList_ReplacesWithEmptyCollection()
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities") { Id = 1 };
        dataList.ReplaceItems([(
            new Dictionary<string, string> { ["default"] = "Los Angeles" },
            "LA")]);
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, []),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MissingDefaultLabelKey_ReturnsInvalid()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities") { Id = 1 };
        dataList.AddCulture(CultureCode.Parse("es"));
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, [
                new(
                    Value: "NYC",
                    Labels: new Dictionary<string, string> { ["es"] = "Nueva York" })
            ]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.Identifier == "Items[0].Labels.default");
    }

    [Fact]
    public async Task Handle_LabelExceedsMaxLength_ReturnsInvalid()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities") { Id = 1 };
        string tooLong = new('x', DataListItem.MAX_LABEL_LENGTH + 1);
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, [Item(tooLong, "NYC")]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e =>
            e.Identifier == "Items[0].Labels.default"
            && e.ErrorMessage.Contains(DataListItem.MAX_LABEL_LENGTH.ToString()));
    }

    [Fact]
    public async Task Handle_MultipleInvalidItems_ReturnsAllValidationErrors()
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities") { Id = 1 };
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);

        var result = await _sut.Handle(
            new ReplaceDataListItemsCommand(1, [
                Item("", ""),
                new(
                    Value: "LA",
                    Labels: new Dictionary<string, string>
                    {
                        ["default"] = "Los Angeles",
                        ["fr"] = "Los Angeles"
                    })
            ]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.Identifier == "Items[0].Labels");
        result.ValidationErrors.Should().Contain(e => e.Identifier == "Items[0].Value");
        result.ValidationErrors.Should().Contain(e => e.Identifier == "Items[1].Labels.fr");
    }
}

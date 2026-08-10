using Endatix.Core.Abstractions;
using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.DataLists.Translations;
using MediatR;

namespace Endatix.Core.Tests.UseCases.DataLists.Translations;

public class ReplaceDataListTranslationsCsvHandlerTests
{
    private readonly IRepository<DataList> _repository;
    private readonly IMediator _mediator;
    private readonly IIdGenerator<long> _idGenerator;
    private readonly ReplaceDataListTranslationsCsvHandler _sut;
    private long _nextId = 100;

    public ReplaceDataListTranslationsCsvHandlerTests()
    {
        _repository = Substitute.For<IRepository<DataList>>();
        _mediator = Substitute.For<IMediator>();
        _idGenerator = Substitute.For<IIdGenerator<long>>();
        _idGenerator.CreateId().Returns(_ => Interlocked.Increment(ref _nextId));
        _sut = new ReplaceDataListTranslationsCsvHandler(_repository, _mediator, _idGenerator);
    }

    private DataList GivenDataList(params string[] locales)
    {
        DataList dataList = new(SampleData.TENANT_ID, "Cities", defaultLocale: "en") { Id = 1 };
        foreach (string locale in locales)
        {
            dataList.AddCulture(CultureCode.Parse(locale));
        }

        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithItemsSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(dataList);

        return dataList;
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
            new ReplaceDataListTranslationsCsvCommand(1, "value,default\r\napple,Apple\r\n"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCsv_ReplacesItemsWithTranslations()
    {
        // Arrange
        DataList dataList = GivenDataList("es");
        dataList.AddItem("Stale", "stale");

        // Act
        var result = await _sut.Handle(
            new ReplaceDataListTranslationsCsvCommand(
                1,
                "value,default,es\r\napple,Apple,Manzana\r\nbanana,Banana,\r\n"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        dataList.Items.Select(i => i.Value).Should().Equal("apple", "banana");
        dataList.Items.First().Labels.Should().BeEquivalentTo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["default"] = "Apple",
            ["es"] = "Manzana"
        });
        dataList.Items.Last().Labels.Should().NotContainKey("es");
        await _repository.Received(1).UpdateAsync(dataList, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCsv_PublishesItemsReplacedEvent()
    {
        // Arrange
        GivenDataList();

        // Act
        await _sut.Handle(
            new ReplaceDataListTranslationsCsvCommand(1, "value,default\r\napple,Apple\r\n"),
            TestContext.Current.CancellationToken);

        // Assert
        await _mediator.Received(1).Publish(
            Arg.Is<DataListUpdatedEvent>(e =>
                e.DataList.Id == 1
                && e.Reason == DataListUpdateReasons.ItemsReplaced),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DefaultLocaleColumn_IsTreatedAsDefaultKey()
    {
        // Arrange
        DataList dataList = GivenDataList();

        // Act
        var result = await _sut.Handle(
            new ReplaceDataListTranslationsCsvCommand(1, "value,en\r\napple,Apple\r\n"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        dataList.Items.Single().Labels.Should().ContainKey("default").WhoseValue.Should().Be("Apple");
    }

    [Fact]
    public async Task Handle_UnknownLocaleColumn_ReturnsInvalid()
    {
        // Arrange
        DataList dataList = GivenDataList("es");

        // Act
        var result = await _sut.Handle(
            new ReplaceDataListTranslationsCsvCommand(1, "value,default,fr\r\napple,Apple,Pomme\r\n"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(e => e.Identifier == "Columns.fr");
        dataList.Items.Should().BeEmpty();
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EnsureLocales_AddsCultureThenImports()
    {
        DataList dataList = GivenDataList();

        var result = await _sut.Handle(
            new ReplaceDataListTranslationsCsvCommand(
                1,
                "value,default,fr,es\r\napple,Apple,Pomme,Manzana\r\n",
                ensureLocales: ["fr", "es"]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        dataList.AvailableLocales.Should().BeEquivalentTo(["fr", "es"]);
        dataList.Items.Single().Labels.Should().BeEquivalentTo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["default"] = "Apple",
            ["fr"] = "Pomme",
            ["es"] = "Manzana"
        });
    }

    [Fact]
    public async Task Handle_EnsureLocales_InvalidCode_ReturnsInvalid()
    {
        GivenDataList();

        var result = await _sut.Handle(
            new ReplaceDataListTranslationsCsvCommand(
                1,
                "value,default\r\napple,Apple\r\n",
                ensureLocales: ["not a culture!"]),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.Identifier.StartsWith("EnsureLocales."));
    }

    [Fact]
    public async Task Handle_MissingDefaultColumn_ReturnsInvalid()
    {
        // Arrange
        GivenDataList("es");

        // Act
        var result = await _sut.Handle(
            new ReplaceDataListTranslationsCsvCommand(1, "value,es\r\napple,Manzana\r\n"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(e => e.Identifier == "Columns.default");
    }

    [Fact]
    public async Task Handle_OverLongLabel_ReturnsInvalid()
    {
        // Arrange
        GivenDataList();
        string label = new('x', DataListItem.MAX_LABEL_LENGTH + 1);

        // Act
        var result = await _sut.Handle(
            new ReplaceDataListTranslationsCsvCommand(1, $"value,default\r\napple,{label}\r\n"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(e => e.Identifier == "Rows[0].default");
    }

    [Fact]
    public async Task Handle_EmptyDefaultCell_ReturnsInvalid()
    {
        // Arrange
        GivenDataList("es");

        // Act
        var result = await _sut.Handle(
            new ReplaceDataListTranslationsCsvCommand(1, "value,default,es\r\napple,,Manzana\r\n"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(e => e.Identifier == "Rows[0].default");
    }

    [Fact]
    public async Task Handle_DuplicateValues_ReturnsInvalid()
    {
        // Arrange
        GivenDataList();

        // Act
        var result = await _sut.Handle(
            new ReplaceDataListTranslationsCsvCommand(1, "value,default\r\napple,Apple\r\napple,Apple 2\r\n"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(e => e.Identifier == "Rows[1].value");
    }

    [Fact]
    public async Task Handle_MalformedCsv_ReturnsInvalid()
    {
        // Arrange
        GivenDataList();

        // Act
        var result = await _sut.Handle(
            new ReplaceDataListTranslationsCsvCommand(1, "default,es\r\nApple,Manzana\r\n"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(e => e.Identifier == "Csv");
    }

    [Fact]
    public async Task Handle_MoreThanMaxRows_ReturnsInvalid()
    {
        GivenDataList();
        string csv = "value,default\r\n" + string.Join(
            "\r\n",
            Enumerable.Range(0, ReplaceDataListTranslationsCsvCommand.MAX_ROWS + 1)
                .Select(i => $"{i},Label{i}"));

        var result = await _sut.Handle(
            new ReplaceDataListTranslationsCsvCommand(1, csv),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(e =>
            e.Identifier == "Csv"
            && e.ErrorMessage.Contains(ReplaceDataListTranslationsCsvCommand.MAX_ROWS.ToString("N0")));
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhitespaceOnlyDefaultLabel_ReturnsInvalid()
    {
        // Quoted whitespace survives CSV parse and BuildItems, then ReplaceItems → NormalizeLabels throws.
        GivenDataList();

        var result = await _sut.Handle(
            new ReplaceDataListTranslationsCsvCommand(1, "value,default\r\napple,\"   \"\r\n"),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(e => e.Identifier == "Csv");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>());
    }
}

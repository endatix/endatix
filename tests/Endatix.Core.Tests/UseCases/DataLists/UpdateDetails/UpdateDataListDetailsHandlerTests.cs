using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.UpdateDetails;
using MediatR;

namespace Endatix.Core.Tests.UseCases.DataLists.UpdateDetails;

public class UpdateDataListDetailsHandlerTests
{
    private readonly IRepository<DataList> _repository;
    private readonly IValueNormalizer _valueNormalizer;
    private readonly IUniqueConstraintViolationChecker _uniqueConstraintViolationChecker;
    private readonly IMediator _mediator;
    private readonly UpdateDataListDetailsHandler _sut;

    public UpdateDataListDetailsHandlerTests()
    {
        _repository = Substitute.For<IRepository<DataList>>();
        _valueNormalizer = Substitute.For<IValueNormalizer>();
        _uniqueConstraintViolationChecker = Substitute.For<IUniqueConstraintViolationChecker>();
        _mediator = Substitute.For<IMediator>();
        _valueNormalizer.Normalize(Arg.Any<string>()).Returns(ci => ci.Arg<string>().Trim().ToUpperInvariant());
        _sut = new UpdateDataListDetailsHandler(
            _repository,
            _valueNormalizer,
            _uniqueConstraintViolationChecker,
            _mediator);
    }

    [Fact]
    public async Task Handle_DataListNotFound_ReturnsNotFound()
    {
        // Arrange
        _repository.GetByIdAsync(1L, Arg.Any<CancellationToken>())
            .Returns((DataList?)null);

        // Act
        var result = await _sut.Handle(
            new UpdateDataListDetailsCommand(1, "Cities", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesNameAndDescription()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities", "Old", "CITIES") { Id = 1 };
        _repository.GetByIdAsync(1L, Arg.Any<CancellationToken>()).Returns(dataList);
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByNormalizedNameSpec>(),
                Arg.Any<CancellationToken>())
            .Returns((DataList?)null);

        // Act
        var result = await _sut.Handle(
            new UpdateDataListDetailsCommand(1, "Metros", "New desc"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Metros");
        result.Value.Description.Should().Be("New desc");
        dataList.Name.Should().Be("Metros");
        dataList.NormalizedName.Should().Be("METROS");
        await _repository.Received(1).UpdateAsync(dataList, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsAccurateItemsCount()
    {
        // Arrange: the tracked `dataList` used for the mutation never has its
        // Items navigation Included, so its own Items.Count would read 0 --
        // regression guard that the handler re-fetches the count instead of
        // trusting the unloaded collection on the tracked entity.
        DataList dataList = new(SampleData.TENANT_ID, "Cities", "Old", "CITIES") { Id = 1 };
        _repository.GetByIdAsync(1L, Arg.Any<CancellationToken>()).Returns(dataList);
        _repository.FirstOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByIdWithoutItemsToDtoSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(new DataListDto(1, "Metros", "New desc", DateTime.UtcNow, DateTime.UtcNow, true, 42, "en", [], []));

        // Act
        var result = await _sut.Handle(
            new UpdateDataListDetailsCommand(1, "Metros", "New desc"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.ItemsCount.Should().Be(42);
    }

    [Fact]
    public async Task Handle_ValidRequest_PublishesMetadataUpdatedEvent()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities", null, "CITIES") { Id = 1 };
        _repository.GetByIdAsync(1L, Arg.Any<CancellationToken>()).Returns(dataList);

        // Act
        await _sut.Handle(
            new UpdateDataListDetailsCommand(1, null, "Updated"),
            TestContext.Current.CancellationToken);

        // Assert
        await _mediator.Received(1).Publish(
            Arg.Is<DataListUpdatedEvent>(e =>
                e.DataList.Id == 1
                && e.Reason == DataListUpdateReasons.MetadataUpdated),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateNameExists_ReturnsInvalid()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities", null, "CITIES") { Id = 1 };
        DataList other = new(SampleData.TENANT_ID, "Metros", null, "METROS") { Id = 2 };
        _repository.GetByIdAsync(1L, Arg.Any<CancellationToken>()).Returns(dataList);
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByNormalizedNameSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(other);

        // Act
        var result = await _sut.Handle(
            new UpdateDataListDetailsCommand(1, "Metros", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(error =>
            error.Identifier == nameof(UpdateDataListDetailsCommand.Name)
            && error.ErrorCode == UpdateDataListDetailsHandler.DuplicateNameErrorCode);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SameNormalizedName_SkipsUniquenessCheck()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities", "Old", "CITIES") { Id = 1 };
        _repository.GetByIdAsync(1L, Arg.Any<CancellationToken>()).Returns(dataList);

        // Act
        var result = await _sut.Handle(
            new UpdateDataListDetailsCommand(1, "cities", "New"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.Name.Should().Be("cities");
        await _repository.DidNotReceive()
            .SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByNormalizedNameSpec>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).UpdateAsync(dataList, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RaceConditionUniqueViolation_ReturnsInvalid()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities", null, "CITIES") { Id = 1 };
        _repository.GetByIdAsync(1L, Arg.Any<CancellationToken>()).Returns(dataList);
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByNormalizedNameSpec>(),
                Arg.Any<CancellationToken>())
            .Returns((DataList?)null);
        _repository.UpdateAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new Exception("db failed"));
        _uniqueConstraintViolationChecker.AnalyzeUniqueConstraint(Arg.Any<Exception>())
            .Returns(new UniqueConstraintViolationResult(true, DataList.UniqueConstraints.NamePerTenant, null));

        // Act
        var result = await _sut.Handle(
            new UpdateDataListDetailsCommand(1, "Metros", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(error =>
            error.ErrorCode == UpdateDataListDetailsHandler.DuplicateNameErrorCode);
    }

    [Fact]
    public async Task Handle_UniqueViolationNotNameConstraint_ReturnsGenericConflict()
    {
        // Arrange: a unique-constraint violation whose constraint/column
        // doesn't match the name constraint should not be mislabeled as a
        // duplicate-name error.
        DataList dataList = new(SampleData.TENANT_ID, "Cities", null, "CITIES") { Id = 1 };
        _repository.GetByIdAsync(1L, Arg.Any<CancellationToken>()).Returns(dataList);
        _repository.SingleOrDefaultAsync(
                Arg.Any<DataListsSpecifications.ByNormalizedNameSpec>(),
                Arg.Any<CancellationToken>())
            .Returns((DataList?)null);
        _repository.UpdateAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new Exception("db failed"));
        _uniqueConstraintViolationChecker.AnalyzeUniqueConstraint(Arg.Any<Exception>())
            .Returns(new UniqueConstraintViolationResult(true, "IX_SomeOtherConstraint", "SomeOtherColumn"));

        // Act
        var result = await _sut.Handle(
            new UpdateDataListDetailsCommand(1, "Metros", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().NotContain(error =>
            error.ErrorCode == UpdateDataListDetailsHandler.DuplicateNameErrorCode);
    }

    [Fact]
    public async Task Handle_WhitespaceOnlyDescription_NormalizesToEmptyString()
    {
        // Arrange: matches CreateDataListHandler's `Description?.Trim()`
        // behavior -- whitespace-only input becomes "", not null.
        DataList dataList = new(SampleData.TENANT_ID, "Cities", "Old", "CITIES") { Id = 1 };
        _repository.GetByIdAsync(1L, Arg.Any<CancellationToken>()).Returns(dataList);

        // Act
        var result = await _sut.Handle(
            new UpdateDataListDetailsCommand(1, null, "   "),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        dataList.Description.Should().Be(string.Empty);
    }

    [Fact]
    public async Task Handle_DescriptionOnly_KeepsName()
    {
        // Arrange
        DataList dataList = new(SampleData.TENANT_ID, "Cities", "Old", "CITIES") { Id = 1 };
        _repository.GetByIdAsync(1L, Arg.Any<CancellationToken>()).Returns(dataList);

        // Act
        var result = await _sut.Handle(
            new UpdateDataListDetailsCommand(1, null, "Only desc"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.Name.Should().Be("Cities");
        result.Value.Description.Should().Be("Only desc");
    }

    [Fact]
    public void Constructor_NonPositiveDataListId_ThrowsArgumentException()
    {
        // Act
        Action act = () => _ = new UpdateDataListDetailsCommand(0, "Cities", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}

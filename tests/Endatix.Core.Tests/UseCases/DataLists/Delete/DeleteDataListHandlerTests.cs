using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists.Delete;
using MediatR;

namespace Endatix.Core.Tests.UseCases.DataLists.Delete;

public class DeleteDataListHandlerTests
{
    private readonly IRepository<DataList> _repository;
    private readonly IDataListDependencyChecker _dependencyChecker;
    private readonly IMediator _mediator;
    private readonly DeleteDataListHandler _sut;

    public DeleteDataListHandlerTests()
    {
        _repository = Substitute.For<IRepository<DataList>>();
        _dependencyChecker = Substitute.For<IDataListDependencyChecker>();
        _mediator = Substitute.For<IMediator>();
        _sut = new DeleteDataListHandler(_repository, _dependencyChecker, _mediator);
    }

    [Fact]
    public async Task Handle_DataListHasDependencies_ReturnsInvalid()
    {
        DataList list = new(SampleData.TENANT_ID, "Cities") { Id = 10 };
        _repository.SingleOrDefaultAsync(Arg.Any<Core.Specifications.DataListByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(list);
        _dependencyChecker.HasFormDependenciesAsync(10, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _sut.Handle(new DeleteDataListCommand(10), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_DeletesDataList()
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities") { Id = 10 };
        _repository.SingleOrDefaultAsync(Arg.Any<Core.Specifications.DataListByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);
        _dependencyChecker.HasFormDependenciesAsync(10, Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _sut.Handle(new DeleteDataListCommand(10), TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().Be(dataList);
        result.Value.IsDeleted.Should().BeTrue();

        await _repository.Received(1).UpdateAsync(
            Arg.Is<DataList>(dl =>
                dl.Id == dataList.Id &&
                dl.IsDeleted
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_ValidRequest_PublishesDataListDeletedEvent()
    {
        var dataList = new DataList(SampleData.TENANT_ID, "Cities") { Id = 10 };
        _repository.SingleOrDefaultAsync(Arg.Any<Core.Specifications.DataListByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(dataList);
        _dependencyChecker.HasFormDependenciesAsync(10, Arg.Any<CancellationToken>())
            .Returns(false);

        await _sut.Handle(new DeleteDataListCommand(10), TestContext.Current.CancellationToken);

        await _mediator.Received(1).Publish(
            Arg.Is<DataListDeletedEvent>(e =>
                e.DataList.Id == dataList.Id
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
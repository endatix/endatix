using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.DataLists.Create;

namespace Endatix.Core.Tests.UseCases.DataLists.Create;

public class CreateDataListHandlerTests
{
    private readonly ITenantContext _tenantContext;
    private readonly IRepository<DataList> _repository;
    private readonly IUniqueConstraintViolationChecker _uniqueConstraintViolationChecker;
    private readonly CreateDataListHandler _sut;

    public CreateDataListHandlerTests()
    {
        _tenantContext = Substitute.For<ITenantContext>();
        _repository = Substitute.For<IRepository<DataList>>();
        _uniqueConstraintViolationChecker = Substitute.For<IUniqueConstraintViolationChecker>();
        _sut = new CreateDataListHandler(_tenantContext, _repository, _uniqueConstraintViolationChecker);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsCreated()
    {
        _tenantContext.TenantId.Returns(101);
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByNameSpec>(), Arg.Any<CancellationToken>())
            .Returns((DataList?)null);
        _repository.AddAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>())
            .Returns(info => new DataList(101, info[0]?.ToString()!, "Test Description"));

        var result = await _sut.Handle(
            new CreateDataListCommand("Cities", "Test Description"),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_DuplicateNameExists_ReturnsInvalid()
    {
        _tenantContext.TenantId.Returns(101);
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByNameSpec>(), Arg.Any<CancellationToken>())
            .Returns(new DataList(101, "Cities", null));

        var result = await _sut.Handle(
            new CreateDataListCommand("Cities", null),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
        await _repository.DidNotReceive().AddAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RaceConditionUniqueViolation_ReturnsInvalid()
    {
        _tenantContext.TenantId.Returns(101);
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByNameSpec>(), Arg.Any<CancellationToken>())
            .Returns((DataList?)null);
        _repository.AddAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>())
            .Returns<Task<DataList>>(_ => throw new Exception("db failed"));
        _uniqueConstraintViolationChecker.IsUniqueConstraintViolation(Arg.Any<Exception>())
            .Returns(true);

        var result = await _sut.Handle(
            new CreateDataListCommand("Cities", null),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public async Task Handle_UnauthorizedTenant_ReturnsUnauthorized()
    {
        _tenantContext.TenantId.Returns(0);

        var result = await _sut.Handle(
            new CreateDataListCommand("Cities", null),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Unauthorized);
        await _repository.DidNotReceive().AddAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NegativeTenantId_ReturnsUnauthorized()
    {
        _tenantContext.TenantId.Returns(-1);

        var result = await _sut.Handle(
            new CreateDataListCommand("Cities", null),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Unauthorized);
    }

    [Fact]
    public void Handle_CommandWithNullName_ThrowsArgumentException()
    {
        Action act = () => _ = new CreateDataListCommand(null!, "description");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Handle_CommandWithWhitespaceName_ThrowsArgumentException()
    {
        Action act = () => _ = new CreateDataListCommand("   ", "description");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Handle_ValidCommand_SetsCorrectName()
    {
        _tenantContext.TenantId.Returns(101);
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByNameSpec>(), Arg.Any<CancellationToken>())
            .Returns((DataList?)null);
        _repository.AddAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>())
            .Returns(new DataList(101, "Cities", "description"));

        var result = await _sut.Handle(
            new CreateDataListCommand("Cities", "description"),
            TestContext.Current.CancellationToken);

        result.Value!.Name.Should().Be("Cities");
    }

    [Fact]
    public async Task Handle_ValidCommand_SetsCorrectTenantId()
    {
        const long tenantId = 101;
        _tenantContext.TenantId.Returns(tenantId);
        _repository.SingleOrDefaultAsync(Arg.Any<DataListsSpecifications.ByNameSpec>(), Arg.Any<CancellationToken>())
            .Returns((DataList?)null);
        _repository.AddAsync(Arg.Any<DataList>(), Arg.Any<CancellationToken>())
            .Returns(info => new DataList(tenantId, "Cities"));

        var result = await _sut.Handle(
            new CreateDataListCommand("Cities", null),
            TestContext.Current.CancellationToken);

        result.Value!.TenantId.Should().Be(tenantId);
    }
}

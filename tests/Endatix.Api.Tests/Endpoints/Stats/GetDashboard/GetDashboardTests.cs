using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Stats.GetDashboard;
using Endatix.Core.UseCases.Stats.Models;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using GetDashboardEndpoint = Endatix.Api.Endpoints.Stats.GetDashboard.GetDashboard;

namespace Endatix.Api.Tests.Endpoints.Stats.GetDashboard;

public class GetDashboardTests
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly GetDashboardEndpoint _endpoint;

    public GetDashboardTests()
    {
        _mediator = Substitute.For<IMediator>();
        _tenantContext = Substitute.For<ITenantContext>();
        _endpoint = Factory.Create<GetDashboardEndpoint>(_mediator, _tenantContext);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithDashboard()
    {
        // Arrange
        var tenantId = SampleData.TENANT_ID;
        _tenantContext.TenantId.Returns(tenantId);

        var tenantStats = new TenantStorageStats(tenantId, 100, 50, 1024);
        var formStats = new List<FormStorageStats>();
        var tableStats = new List<TableStorageStats>();
        var dashboard = new StorageDashboardModel(tenantStats, formStats, tableStats);
        var result = Result.Success(dashboard);

        _mediator.Send(Arg.Any<GetStorageDashboardQuery>(), Arg.Any<CancellationToken>()).Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<StorageDashboardModel>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult.Value.TenantStats.Should().Be(tenantStats);
        okResult.Value.FormStats.Should().BeEmpty();
        okResult.Value.TableStats.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_SendsQueryWithTenantIdFromContext()
    {
        // Arrange
        long tenantId = 42;
        _tenantContext.TenantId.Returns(tenantId);
        var tenantStats = new TenantStorageStats(tenantId, 0, 0, 0);
        var result = Result.Success(
            new StorageDashboardModel(tenantStats, [], []));
        _mediator.Send(Arg.Any<GetStorageDashboardQuery>(), Arg.Any<CancellationToken>()).Returns(result);

        // Act
        await _endpoint.ExecuteAsync(CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GetStorageDashboardQuery>(q => q.TenantId == tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTenantIdZero_SendsQueryWithNullTenantId()
    {
        // Arrange
        _tenantContext.TenantId.Returns(0L);
        var tenantStats = new TenantStorageStats(0, 200, 100, 2048);
        var result = Result.Success(
            new StorageDashboardModel(tenantStats, [], []));
        _mediator.Send(Arg.Any<GetStorageDashboardQuery>(), Arg.Any<CancellationToken>()).Returns(result);

        // Act
        await _endpoint.ExecuteAsync(CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GetStorageDashboardQuery>(q => q.TenantId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ErrorResult_ReturnsProblem()
    {
        // Arrange
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        var result = Result<StorageDashboardModel>.Error("Storage stats unavailable.");
        _mediator.Send(Arg.Any<GetStorageDashboardQuery>(), Arg.Any<CancellationToken>()).Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
    }
}

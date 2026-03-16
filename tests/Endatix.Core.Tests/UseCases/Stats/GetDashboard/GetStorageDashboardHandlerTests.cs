using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Stats.GetDashboard;
using Endatix.Core.UseCases.Stats.Models;

namespace Endatix.Core.Tests.UseCases.Stats.GetDashboard;

public class GetStorageDashboardHandlerTests
{
    private readonly IStorageStatsRepository _storageStatsRepository;
    private readonly GetStorageDashboardHandler _handler;

    public GetStorageDashboardHandlerTests()
    {
        _storageStatsRepository = Substitute.For<IStorageStatsRepository>();
        _handler = new GetStorageDashboardHandler(_storageStatsRepository);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsStorageDashboardModel()
    {
        // Arrange
        var tenantId = SampleData.TENANT_ID;
        var request = new GetStorageDashboardQuery(tenantId);
        var tenantStats = new TenantStorageStats(tenantId, 100, 50, 1024);
        var formStats = new List<FormStorageStats>
        {
            new(tenantId, 1, "Form A", 10, 5, 256)
        };
        var tableStats = new List<TableStorageStats>
        {
            new("Submissions", 1000, 200, 1200)
        };

        _storageStatsRepository.GetTenantStats(tenantId, Arg.Any<CancellationToken>()).Returns(tenantStats);
        _storageStatsRepository.GetFormStats(tenantId, Arg.Any<CancellationToken>()).Returns(formStats);
        _storageStatsRepository.GetTableStats(Arg.Any<CancellationToken>()).Returns(tableStats);

        // Act
        var result = await _handler.Handle(request, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.TenantStats.Should().Be(tenantStats);
        result.Value.FormStats.Should().BeEquivalentTo(formStats);
        result.Value.TableStats.Should().BeEquivalentTo(tableStats);
    }

    [Fact]
    public async Task Handle_CallsRepositoryWithCorrectTenantId()
    {
        // Arrange
        long tenantId = 42;
        var request = new GetStorageDashboardQuery(tenantId);
        var tenantStats = new TenantStorageStats(tenantId, 0, 0, 0);
        _storageStatsRepository.GetTenantStats(Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(tenantStats);
        _storageStatsRepository.GetFormStats(Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(new List<FormStorageStats>());
        _storageStatsRepository.GetTableStats(Arg.Any<CancellationToken>()).Returns(new List<TableStorageStats>());

        // Act
        await _handler.Handle(request, TestContext.Current.CancellationToken);

        // Assert
        await _storageStatsRepository.Received(1).GetTenantStats(tenantId, Arg.Any<CancellationToken>());
        await _storageStatsRepository.Received(1).GetFormStats(tenantId, Arg.Any<CancellationToken>());
        await _storageStatsRepository.Received(1).GetTableStats(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTenantIdNull_CallsRepositoryWithNull()
    {
        // Arrange
        var request = new GetStorageDashboardQuery(null);
        var tenantStats = new TenantStorageStats(0, 200, 100, 2048);
        _storageStatsRepository.GetTenantStats(null, Arg.Any<CancellationToken>()).Returns(tenantStats);
        _storageStatsRepository.GetFormStats(null, Arg.Any<CancellationToken>()).Returns(new List<FormStorageStats>());
        _storageStatsRepository.GetTableStats(Arg.Any<CancellationToken>()).Returns(new List<TableStorageStats>());

        // Act
        var result = await _handler.Handle(request, TestContext.Current.CancellationToken);

        // Assert
        await _storageStatsRepository.Received(1).GetTenantStats(null, Arg.Any<CancellationToken>());
        await _storageStatsRepository.Received(1).GetFormStats(null, Arg.Any<CancellationToken>());
        result.Value.TenantStats.Should().Be(tenantStats);
    }
}

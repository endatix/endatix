using Endatix.Api.Endpoints.Admin.Tenants;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformTenants;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using ListPlatformTenantsEndpoint = Endatix.Api.Endpoints.Admin.Tenants.List;

namespace Endatix.Api.Tests.Endpoints.Admin.Tenants;

public sealed class ListTests
{
    private readonly IListPlatformTenants _listPlatformTenants;
    private readonly ListPlatformTenantsEndpoint _endpoint;

    public ListTests()
    {
        _listPlatformTenants = Substitute.For<IListPlatformTenants>();
        _endpoint = Factory.Create<ListPlatformTenantsEndpoint>(_listPlatformTenants);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResultIsSuccess_ReturnsOkWithPagedTenants()
    {
        // Arrange
        var request = new ListPlatformTenantsRequest { Page = 1, PageSize = 10 };
        DateTime createdAt = new(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        IReadOnlyList<PlatformTenantListItem> tenants =
        [
            new(1, "Acme", "Primary tenant", createdAt, null, 3, 12),
            new(2, "Beta", null, createdAt, createdAt, 0, 1),
        ];
        var result = Result.Success(
            new Paged<PlatformTenantListItem>(1, 10, 2, 1, tenants));
        _listPlatformTenants
            .ExecuteAsync(1, 10, null, Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result.As<Ok<Paged<PlatformTenantListItem>>>();
        okResult.Value.Should().NotBeNull();
        okResult.Value!.TotalRecords.Should().Be(2);
        okResult.Value.Items.Should().HaveCount(2);
        okResult.Value.Items.First().Name.Should().Be("Acme");
        okResult.Value.Items.First().FormsCount.Should().Be(3);
        okResult.Value.Items.Last().SubmissionsCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_MapsRequestPagingAndSearchToQuery()
    {
        // Arrange
        var request = new ListPlatformTenantsRequest
        {
            Page = 2,
            PageSize = 25,
            Search = "acme",
        };
        _listPlatformTenants
            .ExecuteAsync(2, 25, "acme", Arg.Any<CancellationToken>())
            .Returns(Result.Success(Paged<PlatformTenantListItem>.Empty(25)));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _listPlatformTenants.Received(1).ExecuteAsync(
            2,
            25,
            "acme",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPagingOmitted_UsesSharedDefaults()
    {
        // Arrange
        var request = new ListPlatformTenantsRequest();
        _listPlatformTenants
            .ExecuteAsync(
                PagedRequestLimits.DEFAULT_PAGE,
                PagedRequestLimits.DEFAULT_PAGE_SIZE,
                null,
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(
                Paged<PlatformTenantListItem>.Empty(PagedRequestLimits.DEFAULT_PAGE_SIZE)));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _listPlatformTenants.Received(1).ExecuteAsync(
            PagedRequestLimits.DEFAULT_PAGE,
            PagedRequestLimits.DEFAULT_PAGE_SIZE,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_EmptyList_ReturnsOkWithEmptyPage()
    {
        // Arrange
        var request = new ListPlatformTenantsRequest { Page = 1, PageSize = 10 };
        _listPlatformTenants
            .ExecuteAsync(1, 10, null, Arg.Any<CancellationToken>())
            .Returns(Result.Success(Paged<PlatformTenantListItem>.Empty(10)));

        // Act
        var response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result.As<Ok<Paged<PlatformTenantListItem>>>();
        okResult.Value!.Items.Should().BeEmpty();
        okResult.Value.TotalRecords.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResultIsInvalid_ReturnsProblemHttpResult()
    {
        // Arrange
        var request = new ListPlatformTenantsRequest { Page = 1, PageSize = 10 };
        _listPlatformTenants
            .ExecuteAsync(1, 10, null, Arg.Any<CancellationToken>())
            .Returns(Result<Paged<PlatformTenantListItem>>.Invalid(new ValidationError("Invalid request")));

        // Act
        var response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result.As<ProblemHttpResult>();
        problemResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}

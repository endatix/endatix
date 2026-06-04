using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Endpoints.Users;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities.Identity;
using Endatix.Core.UseCases.Identity.ListUsers;
using ListUsersEndpoint = Endatix.Api.Endpoints.Users.List;

namespace Endatix.Api.Tests.Endpoints.Users;

public class ListTests
{
    private readonly IMediator _mediator;
    private readonly ListUsersEndpoint _endpoint;

    public ListTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<ListUsersEndpoint>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResultIsError_ReturnsProblemResult()
    {
        // Arrange
        var request = new ListUsersRequest { Page = 1, PageSize = 10 };
        var errorResult = Result<Paged<UserWithRoles>>.Error(
            new ErrorList(["Service error"], null));
        _mediator.Send(Arg.Any<ListUsersQuery>(), Arg.Any<CancellationToken>()).Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenResultIsSuccess_ReturnsOkWithListUsersResponse()
    {
        // Arrange
        var request = new ListUsersRequest { Page = 1, PageSize = 20 };
        var usersWithRoles = new List<UserWithRoles>
        {
            new()
            {
                Id = 1,
                UserName = "alice",
                Email = "alice@example.com",
                IsVerified = true,
                Roles = ["Admin", "Creator"]
            },
            new()
            {
                Id = 2,
                UserName = "bob",
                Email = "bob@example.com",
                IsVerified = false,
                Roles = []
            }
        };
        var result = Result.Success(new Paged<UserWithRoles>(
            1,
            20,
            42,
            3,
            usersWithRoles));
        _mediator.Send(Arg.Any<ListUsersQuery>(), Arg.Any<CancellationToken>()).Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<Paged<ListUsersResponse>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult.Value!.Page.Should().Be(1);
        okResult.Value.PageSize.Should().Be(20);
        okResult.Value.TotalRecords.Should().Be(42);
        okResult.Value.TotalPages.Should().Be(3);
        okResult.Value.Items.Should().HaveCount(2);
        okResult.Value.Items.First().UserName.Should().Be("alice");
        okResult.Value.Items.First().Roles.Should().Contain("Admin").And.Contain("Creator");
        okResult.Value.Items.Last().UserName.Should().Be("bob");
        okResult.Value.Items.Last().Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToListUsersQuery()
    {
        // Arrange
        var request = new ListUsersRequest
        {
            Page = 2,
            PageSize = 50,
            Search = "alice",
            Role = "Admin",
            Status = "pending"
        };
        var result = Result.Success(Paged<UserWithRoles>.Empty(50));
        _mediator.Send(Arg.Any<ListUsersQuery>(), Arg.Any<CancellationToken>()).Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ListUsersQuery>(q =>
                q.Page == 2 &&
                q.PageSize == 50 &&
                q.Search == "alice" &&
                q.Role == "Admin" &&
                q.Status == "pending"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_EmptyList_ReturnsOkWithEmptyList()
    {
        // Arrange
        var request = new ListUsersRequest();
        var result = Result.Success(Paged<UserWithRoles>.Empty(ListUsersQuery.DefaultPageSize));
        _mediator.Send(Arg.Any<ListUsersQuery>(), Arg.Any<CancellationToken>()).Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<Paged<ListUsersResponse>>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult.Value!.Items.Should().BeEmpty();
    }
}

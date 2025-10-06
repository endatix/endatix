using Endatix.Api.Endpoints.Users;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.GetUserRoles;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Users;

public class GetUserRolesTests
{
    private readonly IMediator _mediator;
    private readonly GetUserRoles _endpoint;

    public GetUserRolesTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<GetUserRoles>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidUserId_ReturnsOkResultWithRoles()
    {
        // Arrange
        var request = new GetUserRolesRequest { UserId = 1 };
        var roles = new List<string> { "Admin", "Panelist" } as IList<string>;
        var successResult = Result.Success(roles);

        _mediator.Send(Arg.Is<GetUserRolesQuery>(q => q.UserId == request.UserId))
            .Returns(successResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response!.Result.As<Ok<IList<string>>>();
        _endpoint.HttpContext.Response.StatusCode.Should().Be(200);
        response.Should().NotBeNull();
        response.Result.Should().BeAssignableTo<Ok<IList<string>>>();
        okResult.Should().NotBeNull();
        okResult.Value.Should().NotBeNull();
        okResult.Value.Should().HaveCount(2);
        okResult.Value.Should().Contain("Admin");
        okResult.Value.Should().Contain("Panelist");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidUserId_ReturnsEmptyListWhenNoRoles()
    {
        // Arrange
        var request = new GetUserRolesRequest { UserId = 1 };
        var roles = new List<string>() as IList<string>;
        var successResult = Result.Success(roles);

        _mediator.Send(Arg.Is<GetUserRolesQuery>(q => q.UserId == request.UserId))
            .Returns(successResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response!.Result.As<Ok<IList<string>>>();
        _endpoint.HttpContext.Response.StatusCode.Should().Be(200);
        okResult.Value.Should().NotBeNull();
        okResult.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFound_ReturnsProblemResult()
    {
        // Arrange
        var request = new GetUserRolesRequest { UserId = 999 };
        var notFoundResult = Result<IList<string>>.NotFound("User with ID 999 not found.");

        _mediator.Send(Arg.Is<GetUserRolesQuery>(q => q.UserId == request.UserId))
            .Returns(notFoundResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult?.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        problemResult?.ProblemDetails.Detail.Should().Contain("User with ID 999 not found.");
    }
}

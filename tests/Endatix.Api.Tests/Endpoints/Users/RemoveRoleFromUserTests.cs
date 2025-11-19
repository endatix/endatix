using Endatix.Api.Endpoints.Users;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.RemoveRole;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Users;

public class RemoveRoleFromUserTests
{
    private readonly IMediator _mediator;
    private readonly RemoveRoleFromUser _endpoint;

    public RemoveRoleFromUserTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<RemoveRoleFromUser>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new RemoveRoleRequest { UserId = 1, RoleName = "Admin" };
        var command = new RemoveRoleCommand(request.UserId, request.RoleName);
        var successResult = Result.Success("Role 'Admin' successfully removed from user.");

        _mediator.Send(Arg.Is<RemoveRoleCommand>(c => c.UserId == request.UserId && c.RoleName == request.RoleName))
            .Returns(successResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response!.Result.As<Ok<RemoveRoleResponse>>();
        _endpoint.HttpContext.Response.StatusCode.Should().Be(200);
        response.Should().NotBeNull();
        response.Result.Should().BeAssignableTo<Ok<RemoveRoleResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value.Should().NotBeNull();
        okResult.Value?.Message.Should().Be("Role 'Admin' successfully removed from user.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFound_ReturnsProblemResult()
    {
        // Arrange
        var request = new RemoveRoleRequest { UserId = 999, RoleName = "Admin" };
        var notFoundResult = Result<string>.NotFound("User with ID 999 not found.");

        _mediator.Send(Arg.Is<RemoveRoleCommand>(c => c.UserId == request.UserId && c.RoleName == request.RoleName))
            .Returns(notFoundResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult?.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        problemResult?.ProblemDetails.Detail.Should().Contain("User with ID 999 not found.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotHaveRole_ReturnsProblemResult()
    {
        // Arrange
        var request = new RemoveRoleRequest { UserId = 1, RoleName = "Admin" };
        var invalidResult = Result.Invalid(new ValidationError
        {
            Identifier = "roleName",
            ErrorMessage = "User does not have role 'Admin'."
        });

        _mediator.Send(Arg.Is<RemoveRoleCommand>(c => c.UserId == request.UserId && c.RoleName == request.RoleName))
            .Returns(invalidResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemResult?.ProblemDetails.Detail.Should().Contain("User does not have role 'Admin'.");
    }
}

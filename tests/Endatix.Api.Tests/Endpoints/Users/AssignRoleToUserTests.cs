using Endatix.Api.Endpoints.Users;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.AssignRole;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Users;

public class AssignRoleToUserTests
{
    private readonly IMediator _mediator;
    private readonly AssignRoleToUser _endpoint;

    public AssignRoleToUserTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<AssignRoleToUser>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new AssignRoleRequest { UserId = 1, RoleName = "Admin" };
        var command = new AssignRoleCommand(request.UserId, request.RoleName);
        var successResult = Result.Success("Role 'Admin' successfully assigned to user.");

        _mediator.Send(Arg.Is<AssignRoleCommand>(c => c.UserId == request.UserId && c.RoleName == request.RoleName))
            .Returns(successResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response!.Result.As<Ok<AssignRoleResponse>>();
        _endpoint.HttpContext.Response.StatusCode.Should().Be(200);
        response.Should().NotBeNull();
        response.Result.Should().BeAssignableTo<Ok<AssignRoleResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value.Should().NotBeNull();
        okResult.Value?.Message.Should().Be("Role 'Admin' successfully assigned to user.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFound_ReturnsProblemResult()
    {
        // Arrange
        var request = new AssignRoleRequest { UserId = 999, RoleName = "Admin" };
        var notFoundResult = Result<string>.NotFound("User with ID 999 not found.");

        _mediator.Send(Arg.Is<AssignRoleCommand>(c => c.UserId == request.UserId && c.RoleName == request.RoleName))
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
    public async Task ExecuteAsync_WhenUserAlreadyHasRole_ReturnsProblemResult()
    {
        // Arrange
        var request = new AssignRoleRequest { UserId = 1, RoleName = "Admin" };
        var invalidResult = Result.Invalid(new ValidationError
        {
            Identifier = "roleName",
            ErrorMessage = "User already has role 'Admin'."
        });

        _mediator.Send(Arg.Is<AssignRoleCommand>(c => c.UserId == request.UserId && c.RoleName == request.RoleName))
            .Returns(invalidResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemResult?.ProblemDetails.Detail.Should().Contain("User already has role 'Admin'.");
    }
}

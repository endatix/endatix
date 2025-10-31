using Endatix.Api.Endpoints.Roles;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.DeleteRole;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Roles;

public class DeleteRoleTests
{
    private readonly IMediator _mediator;
    private readonly DeleteRole _endpoint;

    public DeleteRoleTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<DeleteRole>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new DeleteRoleRequest
        {
            RoleName = "Manager"
        };

        var successMessage = "Role 'Manager' successfully deleted.";
        var successResult = Result.Success(successMessage);

        _mediator.Send(Arg.Is<DeleteRoleCommand>(c =>
            c.RoleName == request.RoleName))
            .Returns(successResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        response.Should().NotBeNull();
        response.Result.Should().BeAssignableTo<Ok<DeleteRoleResponse>>();
        var okResult = response!.Result.As<Ok<DeleteRoleResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value.Should().NotBeNull();
        okResult.Value?.Message.Should().Be(successMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoleNotFound_ReturnsProblemResult()
    {
        // Arrange
        var request = new DeleteRoleRequest
        {
            RoleName = "NonExistent"
        };

        var notFoundResult = Result<string>.NotFound("Role 'NonExistent' not found for this tenant.");

        _mediator.Send(Arg.Is<DeleteRoleCommand>(c => c.RoleName == request.RoleName))
            .Returns(notFoundResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult?.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        problemResult?.ProblemDetails.Detail.Should().Contain("Role 'NonExistent' not found for this tenant.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSystemDefinedRole_ReturnsProblemResult()
    {
        // Arrange
        var request = new DeleteRoleRequest
        {
            RoleName = "Admin"
        };

        var invalidResult = Result<string>.Invalid(new ValidationError
        {
            Identifier = "roleName",
            ErrorMessage = "Cannot delete system-defined role 'Admin'."
        });

        _mediator.Send(Arg.Is<DeleteRoleCommand>(c => c.RoleName == request.RoleName))
            .Returns(invalidResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemResult?.ProblemDetails.Detail.Should().Contain("Cannot delete system-defined role 'Admin'.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoleAssignedToUsers_ReturnsProblemResult()
    {
        // Arrange
        var request = new DeleteRoleRequest
        {
            RoleName = "Manager"
        };

        var invalidResult = Result<string>.Invalid(new ValidationError
        {
            Identifier = "roleName",
            ErrorMessage = "Cannot delete role 'Manager' because it is assigned to one or more users."
        });

        _mediator.Send(Arg.Is<DeleteRoleCommand>(c => c.RoleName == request.RoleName))
            .Returns(invalidResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemResult?.ProblemDetails.Detail.Should().Contain("Cannot delete role 'Manager' because it is assigned to one or more users.");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyRoleName_ThrowsArgumentException()
    {
        // Arrange
        var request = new DeleteRoleRequest
        {
            RoleName = ""
        };

        // Act & Assert
        // The guard clause in DeleteRoleCommand constructor throws before reaching the mediator
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _endpoint.ExecuteAsync(request, default));
    }
}

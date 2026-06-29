using Endatix.Api.Endpoints.Users;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.ReplaceUserRoles;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Users;

public class ReplaceUserRolesTests
{
    private readonly IMediator _mediator;
    private readonly ReplaceUserRoles _endpoint;

    public ReplaceUserRolesTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<ReplaceUserRoles>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new ReplaceUserRolesRequest
        {
            UserId = 1,
            RoleNames = ["Admin", "Creator"]
        };
        var successResult = Result.Success("User roles updated.");

        _mediator.Send(Arg.Is<ReplaceUserRolesCommand>(command =>
                command.UserId == request.UserId &&
                command.RoleNames.SequenceEqual(request.RoleNames)), TestContext.Current.CancellationToken)
            .Returns(successResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response!.Result.As<Ok<UserOperation>>();
        _endpoint.HttpContext.Response.StatusCode.Should().Be(200);
        response.Result.Should().BeAssignableTo<Ok<UserOperation>>();
        okResult.Value.Should().NotBeNull();
        okResult.Value?.IsSuccess.Should().BeTrue();
        okResult.Value?.Message.Should().Be("User roles updated.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoleIsInvalid_ReturnsProblemResult()
    {
        // Arrange
        var request = new ReplaceUserRolesRequest
        {
            UserId = 1,
            RoleNames = ["MissingRole"]
        };
        var invalidResult = Result.Invalid(new ValidationError
        {
            Identifier = "roleNames",
            ErrorMessage = "The following roles do not exist: MissingRole"
        });

        _mediator.Send(Arg.Is<ReplaceUserRolesCommand>(command =>
                command.UserId == request.UserId &&
                command.RoleNames.SequenceEqual(request.RoleNames)), TestContext.Current.CancellationToken)
            .Returns(invalidResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemResult?.ProblemDetails.Detail.Should().Contain("The following roles do not exist: MissingRole");
    }
}

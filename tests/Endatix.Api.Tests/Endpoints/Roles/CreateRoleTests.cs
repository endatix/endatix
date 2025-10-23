using Endatix.Api.Endpoints.Roles;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.CreateRole;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Roles;

public class CreateRoleTests
{
    private readonly IMediator _mediator;
    private readonly CreateRole _endpoint;

    public CreateRoleTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<CreateRole>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_ReturnsCreatedResult()
    {
        // Arrange
        var request = new CreateRoleRequest
        {
            Name = "Manager",
            Description = "Manager role",
            Permissions = new List<string> { "forms.read", "forms.write" }
        };

        var roleId = "123456789";
        var successResult = Result<string>.Created(roleId);

        _mediator.Send(Arg.Is<CreateRoleCommand>(c =>
            c.Name == request.Name &&
            c.Description == request.Description &&
            c.Permissions.Count == 2))
            .Returns(successResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        response.Should().NotBeNull();
        response.Result.Should().BeAssignableTo<Created<CreateRoleResponse>>();
        var createdResult = response!.Result.As<Created<CreateRoleResponse>>();
        createdResult.Should().NotBeNull();
        createdResult.Value.Should().NotBeNull();
        createdResult.Value?.Message.Should().Be(roleId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoleAlreadyExists_ReturnsProblemResult()
    {
        // Arrange
        var request = new CreateRoleRequest
        {
            Name = "Admin",
            Permissions = new List<string> { "forms.read" }
        };

        var invalidResult = Result<string>.Invalid(new ValidationError
        {
            Identifier = "name",
            ErrorMessage = "Role 'Admin' already exists for this tenant."
        });

        _mediator.Send(Arg.Is<CreateRoleCommand>(c => c.Name == request.Name))
            .Returns(invalidResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemResult?.ProblemDetails.Detail.Should().Contain("Role 'Admin' already exists for this tenant.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionsDontExist_ReturnsProblemResult()
    {
        // Arrange
        var request = new CreateRoleRequest
        {
            Name = "Manager",
            Permissions = new List<string> { "invalid.permission", "another.invalid" }
        };

        var invalidResult = Result<string>.Invalid(new ValidationError
        {
            Identifier = "permissionNames",
            ErrorMessage = "The following permissions do not exist: invalid.permission, another.invalid"
        });

        _mediator.Send(Arg.Is<CreateRoleCommand>(c => c.Name == request.Name))
            .Returns(invalidResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemResult?.ProblemDetails.Detail.Should().Contain("The following permissions do not exist");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyPermissionsList_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateRoleRequest
        {
            Name = "Manager",
            Permissions = new List<string>()
        };

        // Act & Assert
        // The guard clause in CreateRoleCommand constructor throws before reaching the mediator
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _endpoint.ExecuteAsync(request, default));
    }
}

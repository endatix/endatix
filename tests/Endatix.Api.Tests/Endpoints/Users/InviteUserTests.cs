using Endatix.Api.Endpoints.Users;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.InviteUser;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Users;

public sealed class InviteUserTests
{
    private readonly IMediator _mediator;
    private readonly IRoleManagementService _roleManagementService;
    private readonly InviteUser _endpoint;

    public InviteUserTests()
    {
        _mediator = Substitute.For<IMediator>();
        _roleManagementService = Substitute.For<IRoleManagementService>();
        _endpoint = Factory.Create<InviteUser>(_mediator, _roleManagementService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvitationSucceeds_ReturnsOkResult()
    {
        // Arrange
        InviteUserRequest request = new()
        {
            Email = "invitee@endatix.com",
            Roles = ["Respondent"]
        };
        User user = new(123L, "invitee@endatix.com", "invitee@endatix.com", false);

        _mediator
            .Send(
                Arg.Is<InviteUserCommand>(command =>
                    command.Email == request.Email &&
                    command.RoleNames.SequenceEqual(request.Roles)),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));

        _roleManagementService
            .GetUserRolesAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IList<string>>(["Respondent"]));

        // Act
        var response =
            await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        var okResult = response.Result.As<Ok<InviteUserResponse>>();
        okResult.Value.Should().NotBeNull();
        okResult.Value!.Id.Should().Be(user.Id);
        okResult.Value.Email.Should().Be(user.Email);
        okResult.Value.IsVerified.Should().BeFalse();
        okResult.Value.Roles.Should().ContainSingle("Respondent");
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvitationFails_ReturnsProblemResult()
    {
        // Arrange
        InviteUserRequest request = new()
        {
            Email = "invitee@endatix.com",
            Roles = []
        };

        _mediator
            .Send(Arg.Any<InviteUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<User>.Conflict("User already exists."));

        // Act
        var response =
            await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        var problemResult = response.Result.As<ProblemHttpResult>();
        problemResult.StatusCode.Should().Be(409);
    }

    [Fact]
    public void InviteUserValidator_WhenEmailIsInvalid_ReturnsValidationError()
    {
        // Arrange
        InviteUserValidator validator = new();
        InviteUserRequest request = new()
        {
            Email = "not-an-email",
            Roles = []
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(InviteUserRequest.Email));
    }

    [Fact]
    public void InviteUserValidator_WhenRoleIsEmpty_ReturnsValidationError()
    {
        // Arrange
        InviteUserValidator validator = new();
        InviteUserRequest request = new()
        {
            Email = "invitee@endatix.com",
            Roles = [string.Empty]
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Roles[0]");
    }
}

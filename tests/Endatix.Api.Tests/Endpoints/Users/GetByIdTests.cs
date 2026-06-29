using Endatix.Api.Endpoints.Users;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.GetUserById;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Users;

public sealed class GetByIdTests
{
    private readonly IMediator _mediator;
    private readonly GetById _endpoint;

    public GetByIdTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<GetById>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserExists_ReturnsOkResult()
    {
        // Arrange
        GetUserByIdRequest request = new() { UserId = 1507759960832868352L };
        UserWithRoles user = new()
        {
            Id = request.UserId,
            UserName = "alice",
            Email = "alice@example.com",
            IsVerified = true,
            Roles = ["Admin", "Creator"]
        };

        _mediator
            .Send(
                Arg.Is<GetUserByIdQuery>(query => query.UserId == request.UserId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));

        // Act
        var response = await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        var okResult = response.Result.As<Ok<GetUserByIdResponse>>();
        okResult.Value.Should().NotBeNull();
        okResult.Value!.Id.Should().Be(user.Id);
        okResult.Value.UserName.Should().Be(user.UserName);
        okResult.Value.Email.Should().Be(user.Email);
        okResult.Value.Roles.Should().BeEquivalentTo(user.Roles);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_ReturnsProblemResult()
    {
        // Arrange
        GetUserByIdRequest request = new() { UserId = 1507759960832868352L };
        _mediator
            .Send(
                Arg.Is<GetUserByIdQuery>(query => query.UserId == request.UserId),
                Arg.Any<CancellationToken>())
            .Returns(Result<UserWithRoles>.NotFound("User not found."));

        // Act
        var response = await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public void GetUserByIdValidator_WhenUserIdIsNotPositive_ReturnsValidationError()
    {
        // Arrange
        GetUserByIdValidator validator = new();

        // Act
        var result = validator.Validate(new GetUserByIdRequest { UserId = 0 });

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GetUserByIdRequest.UserId));
    }
}

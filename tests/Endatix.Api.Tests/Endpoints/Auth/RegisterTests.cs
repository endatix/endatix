using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Errors = Microsoft.AspNetCore.Mvc;
using Endatix.Api.Endpoints.Auth;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Register;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Tests.Endpoints.Auth;

public class RegisterTests
{
    private readonly IMediator _mediator;
    private readonly Register _endpoint;

    public RegisterTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<Register>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new RegisterRequest("user@example.com", "Password123!", "Password123!");
        var successResult = Result.Success();

        _mediator.Send(Arg.Any<RegisterCommand>(), Arg.Any<CancellationToken>())
            .Returns(successResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResponse = response!.Result as Ok<RegisterResponse>;

        okResponse.Should().NotBeNull();
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResponse!.Value!.Success.Should().BeTrue();
        okResponse!.Value!.Message.Should().Be("User has been successfully registered");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidRequest_ThrowsError()
    {
        // Arrange
        var request = new RegisterRequest("invalid@example.com", "WeakPass", "WeakPass");
        var errorResult = Result.Invalid();

        _mediator.Send(Arg.Any<RegisterCommand>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badResponse = response!.Result as BadRequest<Errors.ProblemDetails>;

        badResponse.Should().NotBeNull();
        badResponse!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        badResponse!.Value!.Status.Should().Be(StatusCodes.Status400BadRequest);
        badResponse!.Value!.Title.Should().Be("Registration failed. Please check your input and try again.");
    }
}
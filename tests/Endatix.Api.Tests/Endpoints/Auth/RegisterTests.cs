using Endatix.Api.Endpoints.Auth;
using Endatix.Api.Tests.TestExtensions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Register;
using FastEndpoints;
using MediatR;

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
    public async Task HandleAsync_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new RegisterRequest("user@example.com", "Password123!", "Password123!");
        var successResult = Result.Success();

        _mediator.Send(Arg.Any<RegisterCommand>(), Arg.Any<CancellationToken>())
            .Returns(successResult);

        // Act
        await _endpoint.HandleAsync(request, default);
        var response = _endpoint.Response;

        // Assert
        _endpoint.HttpContext.Response.StatusCode.Should().Be(200);
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Message.Should().Be("User has been successfully registered");
    }

    [Fact]
    public async Task HandleAsync_WithInvalidRequest_ThrowsError()
    {
        // Arrange
        var request = new RegisterRequest("invalid@example.com", "WeakPass", "WeakPass");
        var registerCommand = new RegisterCommand(request.Email, request.Password);
        var errorResult = Result.Invalid();

        _mediator.Send(Arg.Any<RegisterCommand>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        Func<Task> act = async () => await _endpoint.HandleAsync(request, default);

        // Assert
        var expectedErrorMessage = "Registration failed. Please check your input and try again.";
        await act.Should().ThrowValidationFailureAsync(expectedErrorMessage);
        _endpoint.ValidationFailed.Should().BeTrue();
        _endpoint.ValidationFailures.Should().Contain(f =>
            f.ErrorMessage == expectedErrorMessage);
    }
}
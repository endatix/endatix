using Endatix.Api.Endpoints.Account;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Account.ForgotPassword;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Account;

public class ForgotPasswordTests
{
    private readonly IMediator _mediator;
    private readonly ForgotPassword _endpoint;

    public ForgotPasswordTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<ForgotPassword>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidEmail_ReturnsOkResult()
    {
        // Arrange
        var request = new ForgotPasswordRequest { Email = "user@example.com" };
        var forgotPasswordCommand = new ForgotPasswordCommand(request.Email);
        var successResult = Result.Success(ForgotPasswordHandler.GENERAL_SUCCESS_MESSAGE);

        _mediator.Send(forgotPasswordCommand)
           .Returns(successResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response!.Result.As<Ok<ForgotPasswordResponse>>();
        _endpoint.HttpContext.Response.StatusCode.Should().Be(200);
        response.Should().NotBeNull();
        response.Result.Should().BeAssignableTo<Ok<ForgotPasswordResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value.Should().NotBeNull();
        okResult.Value?.Message.Should().Be(ForgotPasswordHandler.GENERAL_SUCCESS_MESSAGE);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidEmail_ReturnsProblemResult()
    {
        // Arrange
        var request = new ForgotPasswordRequest { Email = "invalid-email" };
        var forgotPasswordCommand = new ForgotPasswordCommand(request.Email);
        var errorResult = Result.Invalid(new ValidationError("Invalid email format."));

        _mediator.Send(forgotPasswordCommand)
           .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ExecuteAsync_WithSystemErrorDuringEmailSending_ReturnsProblemResult()
    {
        // Arrange
        var request = new ForgotPasswordRequest { Email = "user@example.com" };
        var forgotPasswordCommand = new ForgotPasswordCommand(request.Email);
        var errorResult = Result.Error(ForgotPasswordHandler.FAILED_TO_SEND_EMAIL_MESSAGE);

        _mediator.Send(forgotPasswordCommand)
           .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(500);
        problemResult!.ProblemDetails.Detail.Should().Contain(ForgotPasswordHandler.FAILED_TO_SEND_EMAIL_MESSAGE);
    }
}
using Endatix.Api.Endpoints.Account;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Account.ResetPassword;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Account;

public class ResetPasswordTests
{
    private readonly IMediator _mediator;
    private readonly ResetPassword _endpoint;

    public ResetPasswordTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<ResetPassword>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidResetRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new ResetPasswordRequest 
        { 
            Email = "user@example.com", 
            ResetCode = "valid-reset-code", 
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };
        var resetPasswordCommand = new ResetPasswordCommand(request.Email, request.ResetCode, request.NewPassword);
        var successResult = Result.Success("Password changed successfully");

        _mediator.Send(resetPasswordCommand)
           .Returns(successResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response!.Result.As<Ok<string>>();
        _endpoint.HttpContext.Response.StatusCode.Should().Be(200);
        response.Should().NotBeNull();
        response.Result.Should().BeAssignableTo<Ok<string>>();
        okResult.Should().NotBeNull();
        okResult.Value.Should().Be("Password changed successfully");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidTokenResult_ReturnsProblemResult()
    {
        // Arrange
        var request = new ResetPasswordRequest 
        { 
            Email = "user@example.com", 
            ResetCode = "invalid-reset-code", 
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };
        var resetPasswordCommand = new ResetPasswordCommand(request.Email, request.ResetCode, request.NewPassword);
        var errorResult = Result.Invalid(new ValidationError(
            errorMessage: "Invalid or expired reset code.",
            errorCode: "invalid_token",
            severity: ValidationSeverity.Error,
            identifier: "reset_password"
            ));

        _mediator.Send(resetPasswordCommand)
           .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(400);
        problemResult!.ProblemDetails.Detail.Should().Contain("Invalid or expired reset code.");
        problemResult!.ProblemDetails.Extensions.Should().ContainKey("errorCode");
        problemResult!.ProblemDetails.Extensions["errorCode"].Should().Be("invalid_token");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyResetCode_ReturnsProblemResult()
    {
        // Arrange
        var request = new ResetPasswordRequest 
        { 
            Email = "user@example.com", 
            ResetCode = string.Empty, 
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };
        var resetPasswordCommand = new ResetPasswordCommand(request.Email, request.ResetCode, request.NewPassword);
        var errorResult = Result.Invalid(new ValidationError("Reset code is required."));

        _mediator.Send(resetPasswordCommand)
           .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(400);
        problemResult!.ProblemDetails.Detail.Should().Contain("Reset code is required.");
    }

    [Fact]
    public async Task ExecuteAsync_WithSystemError_ReturnsProblemResult()
    {
        // Arrange
        var request = new ResetPasswordRequest 
        { 
            Email = "user@example.com", 
            ResetCode = "valid-reset-code", 
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };
        var resetPasswordCommand = new ResetPasswordCommand(request.Email, request.ResetCode, request.NewPassword);
        var errorResult = Result.Error("Could not reset password. Please try again or contact support.");

        _mediator.Send(resetPasswordCommand)
           .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(500);
        problemResult!.ProblemDetails.Detail.Should().Contain("Could not reset password. Please try again or contact support.");
    }
}
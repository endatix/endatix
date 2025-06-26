using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http;
using Endatix.Core.Infrastructure.Result;
using Endatix.Api.Endpoints.Auth;
using Endatix.Core.UseCases.Identity.SendVerificationEmail;

namespace Endatix.Api.Tests.Endpoints.Auth;

public class SendVerificationEmailTests
{
    private readonly IMediator _mediator;
    private readonly SendVerificationEmail _endpoint;

    public SendVerificationEmailTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<SendVerificationEmail>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidEmail_ReturnsOkResult()
    {
        // Arrange
        var request = new SendVerificationEmailRequest("test@example.com");
        var successResult = Result.Success();

        _mediator.Send(Arg.Any<SendVerificationEmailCommand>(), Arg.Any<CancellationToken>())
            .Returns(successResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResponse = response!.Result as Ok<string>;

        okResponse.Should().NotBeNull();
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResponse!.Value.Should().Be("Verification email sent successfully");
    }

    [Fact]
    public async Task ExecuteAsync_WithUserNotFound_ReturnsOkResultToPreventEnumeration()
    {
        // Arrange
        var request = new SendVerificationEmailRequest("nonexistent@example.com");
        var successResult = Result.Success();

        _mediator.Send(Arg.Any<SendVerificationEmailCommand>(), Arg.Any<CancellationToken>())
            .Returns(successResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResponse = response!.Result as Ok<string>;

        okResponse.Should().NotBeNull();
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResponse!.Value.Should().Be("Verification email sent successfully");
    }

    [Fact]
    public async Task ExecuteAsync_WithUserAlreadyVerified_ReturnsOkResultToPreventEnumeration()
    {
        // Arrange
        var request = new SendVerificationEmailRequest("verified@example.com");
        var successResult = Result.Success();

        _mediator.Send(Arg.Any<SendVerificationEmailCommand>(), Arg.Any<CancellationToken>())
            .Returns(successResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResponse = response!.Result as Ok<string>;

        okResponse.Should().NotBeNull();
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResponse!.Value.Should().Be("Verification email sent successfully");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new SendVerificationEmailRequest("invalid-email");
        var errorResult = Result.Invalid(new ValidationError("Invalid email address"));

        _mediator.Send(Arg.Any<SendVerificationEmailCommand>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badResponse = response!.Result as BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>;

        badResponse.Should().NotBeNull();
        badResponse!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
} 
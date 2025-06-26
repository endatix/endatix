using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http;
using Endatix.Core.Infrastructure.Result;
using Endatix.Api.Endpoints.Auth;
using Endatix.Core.UseCases.Identity.VerifyEmail;
using Endatix.Core.Entities.Identity;

namespace Endatix.Api.Tests.Endpoints.Auth;

public class VerifyEmailTests
{
    private readonly IMediator _mediator;
    private readonly VerifyEmail _endpoint;

    public VerifyEmailTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<VerifyEmail>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidToken_ReturnsOkResult()
    {
        // Arrange
        var request = new VerifyEmailRequest("valid-token");
        var user = new User(123L, "testuser", "test@example.com", false);
        var successResult = Result.Success(user);

        _mediator.Send(Arg.Any<VerifyEmailCommand>(), Arg.Any<CancellationToken>())
            .Returns(successResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResponse = response!.Result as Ok<string>;

        okResponse.Should().NotBeNull();
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResponse!.Value.Should().Be("123");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new VerifyEmailRequest("invalid-token");
        var errorResult = Result.Invalid(new ValidationError("Invalid or expired verification token"));

        _mediator.Send(Arg.Any<VerifyEmailCommand>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badResponse = response!.Result as BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>;

        badResponse.Should().NotBeNull();
        badResponse!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_WithNotFoundToken_ReturnsNotFound()
    {
        // Arrange
        var request = new VerifyEmailRequest("not-found-token");
        var notFoundResult = Result.NotFound("Verification token not found");

        _mediator.Send(Arg.Any<VerifyEmailCommand>(), Arg.Any<CancellationToken>())
            .Returns(notFoundResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResponse = response!.Result as NotFound;

        notFoundResponse.Should().NotBeNull();
    }
} 
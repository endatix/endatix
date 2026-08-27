using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Endpoints.Auth;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.Register;
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
        var request = new RegisterRequest("user@example.com", "Password123!", "Password123!");
        var successResult = Result.Success();

        _mediator.Send(Arg.Any<RegisterCommand>(), Arg.Any<CancellationToken>())
            .Returns(successResult);

        var response = await _endpoint.ExecuteAsync(request, default);

        var okResponse = response!.Result as Ok<RegisterResponse>;

        okResponse.Should().NotBeNull();
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResponse!.Value!.Success.Should().BeTrue();
        okResponse!.Value!.Message.Should().Be("User has been successfully registered");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidRequest_ReturnsProblem()
    {
        var request = new RegisterRequest("invalid@example.com", "WeakPass", "WeakPass");
        var errorResult = Result.Invalid();

        _mediator.Send(Arg.Any<RegisterCommand>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response!.Result as ProblemHttpResult;

        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemResult!.ProblemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemResult!.ProblemDetails.Title.Should().Be("Registration failed. Please check your input and try again.");
    }

    [Fact]
    public async Task ExecuteAsync_SelfRegistrationDisabled_ReturnsForbidden()
    {
        var request = new RegisterRequest("user@example.com", "Password123!", "Password123!", "xK9mP2qR");
        _mediator.Send(Arg.Any<RegisterCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Forbidden("Self-registration is not enabled for this tenant."));

        var response = await _endpoint.ExecuteAsync(request, default);

        var problem = response!.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTenantSlug_ReturnsNotFound()
    {
        var request = new RegisterRequest("user@example.com", "Password123!", "Password123!", "xK9mP2qR");
        _mediator.Send(Arg.Any<RegisterCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.NotFound("Tenant not found."));

        var response = await _endpoint.ExecuteAsync(request, default);

        var problem = response!.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

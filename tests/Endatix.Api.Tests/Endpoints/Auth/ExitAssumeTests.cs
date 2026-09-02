using Endatix.Api.Endpoints.Auth;
using Endatix.Api.Tests.Endpoints.Admin.Tenants;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.ExitAssume;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Auth;

public sealed class ExitAssumeTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    [Fact]
    public async Task ExecuteAsync_MultiTenancyDisabled_ReturnsNotFoundWithoutSendingCommand()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: false);

        // Act
        var response = await endpoint.ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await _mediator.DidNotReceive().Send(Arg.Any<ExitAssumeCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SessionNotAssumed_ReturnsBadRequest()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<ExitAssumeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthTokensDto>.Invalid(new ValidationError("Not in an assumed tenant session.")));

        // Act
        var response = await endpoint.ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsHomeTokens()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<ExitAssumeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AuthTokensDto(
                new TokenDto("home-access", DateTime.UtcNow.AddMinutes(30)),
                new TokenDto("refresh", DateTime.UtcNow.AddDays(7)))));

        // Act
        var response = await endpoint.ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        var ok = response.Result.As<Ok<TenantSessionResponse>>();
        ok.Value!.AccessToken.Should().Be("home-access");
        ok.Value.RefreshToken.Should().Be("refresh");
    }

    private ExitAssume CreateEndpoint(bool multiTenancyEnabled) =>
        Factory.Create<ExitAssume>(_mediator, MultiTenancyConfiguration.Create(multiTenancyEnabled));
}

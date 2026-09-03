using Endatix.Api.Endpoints.Auth;
using Endatix.Api.Tests.Endpoints.Admin.Tenants;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.AssumeTenant;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Auth;

public sealed class AssumeTenantTests
{
    private const long TargetTenantId = 99;

    private readonly IMediator _mediator = Substitute.For<IMediator>();

    [Fact]
    public async Task ExecuteAsync_MultiTenancyDisabled_ReturnsNotFoundWithoutSendingCommand()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: false);

        // Act
        var response = await endpoint.ExecuteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await _mediator.DidNotReceive().Send(Arg.Any<AssumeTenantCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_TenantNotFound_ReturnsNotFound()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<AssumeTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthTokensDto>.NotFound("Tenant not found."));

        // Act
        var response = await endpoint.ExecuteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyAssumed_ReturnsBadRequest()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<AssumeTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthTokensDto>.Invalid(new ValidationError("Exit the current assumed tenant session first.")));

        // Act
        var response = await endpoint.ExecuteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyInTargetTenant_ReturnsConflict()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<AssumeTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthTokensDto>.Conflict("You cannot assume a tenant you are already in."));

        // Act
        var response = await endpoint.ExecuteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsTokensAndSendsCommand()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<AssumeTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AuthTokensDto(
                new TokenDto("access", DateTime.UtcNow.AddMinutes(15)),
                new TokenDto("refresh", DateTime.UtcNow.AddDays(7)))));

        // Act
        var response = await endpoint.ExecuteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        var ok = response.Result.As<Ok<TenantSessionResponse>>();
        ok.Value!.AccessToken.Should().Be("access");
        ok.Value.RefreshToken.Should().Be("refresh");
        await _mediator.Received(1).Send(
            Arg.Is<AssumeTenantCommand>(command => command.TenantId == TargetTenantId),
            Arg.Any<CancellationToken>());
    }

    private AssumeTenant CreateEndpoint(bool multiTenancyEnabled) =>
        Factory.Create<AssumeTenant>(_mediator, MultiTenancyConfiguration.Create(multiTenancyEnabled));

    private static AssumeTenantRequest ValidRequest() => new() { TenantId = TargetTenantId };
}

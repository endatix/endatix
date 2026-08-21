using Endatix.Api.Endpoints.Auth;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.AssumeTenant;
using Endatix.Core.UseCases.Identity.ExitAssume;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Tests.Endpoints.Admin.Tenants;

namespace Endatix.Api.Tests.Endpoints.Auth;

public sealed class AssumeTenantTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    [Fact]
    public async Task ExecuteAsync_MultiTenancyDisabled_ReturnsNotFoundWithoutSendingCommand()
    {
        var endpoint = Factory.Create<AssumeTenant>(_mediator, MultiTenancyConfiguration.Create(false));

        var response = await endpoint.ExecuteAsync(new AssumeTenantRequest { TenantId = 99 }, TestContext.Current.CancellationToken);

        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await _mediator.DidNotReceive().Send(Arg.Any<AssumeTenantCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsTokens()
    {
        var endpoint = Factory.Create<AssumeTenant>(_mediator, MultiTenancyConfiguration.Create(true));
        _mediator.Send(Arg.Any<AssumeTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AuthTokensDto(
                new TokenDto("access", DateTime.UtcNow.AddMinutes(15)),
                new TokenDto("refresh", DateTime.UtcNow.AddDays(7)))));

        var response = await endpoint.ExecuteAsync(new AssumeTenantRequest { TenantId = 99 }, TestContext.Current.CancellationToken);

        var ok = response.Result.As<Ok<AssumeTenantResponse>>();
        ok.Value!.AccessToken.Should().Be("access");
        ok.Value.RefreshToken.Should().Be("refresh");
        await _mediator.Received(1).Send(
            Arg.Is<AssumeTenantCommand>(command => command.TenantId == 99),
            Arg.Any<CancellationToken>());
    }
}

public sealed class ExitAssumeTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    [Fact]
    public async Task ExecuteAsync_MultiTenancyDisabled_ReturnsNotFoundWithoutSendingCommand()
    {
        var endpoint = Factory.Create<ExitAssume>(_mediator, MultiTenancyConfiguration.Create(false));

        var response = await endpoint.ExecuteAsync(TestContext.Current.CancellationToken);

        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await _mediator.DidNotReceive().Send(Arg.Any<ExitAssumeCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsHomeTokens()
    {
        var endpoint = Factory.Create<ExitAssume>(_mediator, MultiTenancyConfiguration.Create(true));
        _mediator.Send(Arg.Any<ExitAssumeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AuthTokensDto(
                new TokenDto("home-access", DateTime.UtcNow.AddMinutes(30)),
                new TokenDto("refresh", DateTime.UtcNow.AddDays(7)))));

        var response = await endpoint.ExecuteAsync(TestContext.Current.CancellationToken);

        var ok = response.Result.As<Ok<AssumeTenantResponse>>();
        ok.Value!.AccessToken.Should().Be("home-access");
    }
}

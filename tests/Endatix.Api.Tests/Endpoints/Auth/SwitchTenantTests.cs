using Endatix.Api.Endpoints.Auth;
using Endatix.Api.Tests.Endpoints.Admin.Tenants;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.ListMyTenants;
using Endatix.Core.UseCases.Identity.SwitchTenant;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Auth;

public sealed class ListMyTenantsTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    [Fact]
    public async Task ExecuteAsync_MultiTenancyDisabled_ReturnsNotFoundWithoutSendingQuery()
    {
        var endpoint = Factory.Create<ListMyTenants>(_mediator, MultiTenancyConfiguration.Create(false));

        var response = await endpoint.ExecuteAsync(TestContext.Current.CancellationToken);

        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await _mediator.DidNotReceive().Send(Arg.Any<ListMyTenantsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsItems()
    {
        var endpoint = Factory.Create<ListMyTenants>(_mediator, MultiTenancyConfiguration.Create(true));
        IReadOnlyList<UserTenantDto> tenants =
        [
            new(10, "Home", "xK9mP2qR", true),
            new(20, "Other", "aB3dE5fG", false)
        ];
        _mediator.Send(Arg.Any<ListMyTenantsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new UserTenantsDto(tenants)));

        var response = await endpoint.ExecuteAsync(TestContext.Current.CancellationToken);

        var ok = response.Result.As<Ok<UserTenantsResponse>>();
        ok.Value!.Items.Should().HaveCount(2);
        ok.Value.Items[0].IsActive.Should().BeTrue();
    }
}

public sealed class SwitchTenantTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    [Fact]
    public async Task ExecuteAsync_MultiTenancyDisabled_ReturnsNotFoundWithoutSendingCommand()
    {
        var endpoint = Factory.Create<SwitchTenant>(_mediator, MultiTenancyConfiguration.Create(false));

        var response = await endpoint.ExecuteAsync(
            new SwitchTenantRequest { TenantId = 20 },
            TestContext.Current.CancellationToken);

        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await _mediator.DidNotReceive().Send(Arg.Any<SwitchTenantCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsTokens()
    {
        var endpoint = Factory.Create<SwitchTenant>(_mediator, MultiTenancyConfiguration.Create(true));
        _mediator.Send(Arg.Any<SwitchTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AuthTokensDto(
                new TokenDto("switched-access", DateTime.UtcNow.AddMinutes(30)),
                new TokenDto("refresh", DateTime.UtcNow.AddDays(7)))));

        var response = await endpoint.ExecuteAsync(
            new SwitchTenantRequest { TenantId = 20 },
            TestContext.Current.CancellationToken);

        var ok = response.Result.As<Ok<AssumeTenantResponse>>();
        ok.Value!.AccessToken.Should().Be("switched-access");
        await _mediator.Received(1).Send(
            Arg.Is<SwitchTenantCommand>(command => command.TenantId == 20),
            Arg.Any<CancellationToken>());
    }
}

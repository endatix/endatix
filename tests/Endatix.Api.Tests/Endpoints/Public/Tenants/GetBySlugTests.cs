using Endatix.Api.Endpoints.Public.Tenants;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Tenants.GetPublicBySlug;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using GetPublicTenantEndpoint = Endatix.Api.Endpoints.Public.Tenants.GetBySlug;

namespace Endatix.Api.Tests.Endpoints.Public.Tenants;

public sealed class GetBySlugTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly HybridCache _cache = CreateCache();

    [Fact]
    public async Task ExecuteAsync_UnknownSlug_ReturnsNotFound()
    {
        var endpoint = Factory.Create<GetPublicTenantEndpoint>(_mediator, _cache);
        _mediator.Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PublicTenantDto>.NotFound("Tenant not found."));

        var response = await endpoint.ExecuteAsync(
            new GetPublicTenantRequest { Slug = "xK9mP2qR" },
            TestContext.Current.CancellationToken);

        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_NameLikeSlug_ReturnsNotFoundWithoutQuery()
    {
        var endpoint = Factory.Create<GetPublicTenantEndpoint>(_mediator, _cache);

        var response = await endpoint.ExecuteAsync(
            new GetPublicTenantRequest { Slug = "acme" },
            TestContext.Current.CancellationToken);

        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await _mediator.DidNotReceive().Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsDtoWithoutNumericId()
    {
        var endpoint = Factory.Create<GetPublicTenantEndpoint>(_mediator, _cache);
        _mediator.Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PublicTenantDto(
                "xk9mp2qr",
                "Acme",
                true,
                ["endatix"])));

        var response = await endpoint.ExecuteAsync(
            new GetPublicTenantRequest { Slug = "xK9mP2qR" },
            TestContext.Current.CancellationToken);

        var ok = response.Result.As<Ok<PublicTenantModel>>();
        ok.Value!.Slug.Should().Be("xk9mp2qr");
        ok.Value.SelfRegistrationEnabled.Should().BeTrue();
        ok.Value.GetType().GetProperty("Id").Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Success_CachesAndSkipsSecondLookup()
    {
        var endpoint = Factory.Create<GetPublicTenantEndpoint>(_mediator, _cache);
        _mediator.Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PublicTenantDto("xk9mp2qr", "Acme", true, ["endatix"])));
        var request = new GetPublicTenantRequest { Slug = "xk9mp2qr" };

        await endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);
        await endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Success_MixedCaseSlugSharesCacheKey()
    {
        var endpoint = Factory.Create<GetPublicTenantEndpoint>(_mediator, _cache);
        _mediator.Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PublicTenantDto("xk9mp2qr", "Acme", true, ["endatix"])));

        await endpoint.ExecuteAsync(
            new GetPublicTenantRequest { Slug = "xK9mP2qR" },
            TestContext.Current.CancellationToken);
        await endpoint.ExecuteAsync(
            new GetPublicTenantRequest { Slug = "xk9mp2qr" },
            TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NotFound_DoesNotCache()
    {
        var endpoint = Factory.Create<GetPublicTenantEndpoint>(_mediator, _cache);
        _mediator.Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PublicTenantDto>.NotFound("Tenant not found."));
        var request = new GetPublicTenantRequest { Slug = "xk9mp2qr" };

        await endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);
        await endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _mediator.Received(2).Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>());
    }

    private static HybridCache CreateCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
}

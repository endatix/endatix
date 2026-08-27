using Endatix.Api.Endpoints.Public.Tenants;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Tenants.GetPublicBySlug;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
using GetPublicTenantEndpoint = Endatix.Api.Endpoints.Public.Tenants.GetBySlug;

namespace Endatix.Api.Tests.Endpoints.Public.Tenants;

public sealed class GetBySlugTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

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
    public async Task ExecuteAsync_Success_ReturnsDtoWithoutNumericId()
    {
        var endpoint = Factory.Create<GetPublicTenantEndpoint>(_mediator, _cache);
        _mediator.Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PublicTenantDto(
                "xK9mP2qR",
                "Acme",
                true,
                ["endatix"])));

        var response = await endpoint.ExecuteAsync(
            new GetPublicTenantRequest { Slug = "xK9mP2qR" },
            TestContext.Current.CancellationToken);

        var ok = response.Result.As<Ok<PublicTenantModel>>();
        ok.Value!.Slug.Should().Be("xK9mP2qR");
        ok.Value.SelfRegistrationEnabled.Should().BeTrue();
        ok.Value.GetType().GetProperty("Id").Should().BeNull();
    }
}

using Endatix.Api.Endpoints.Public.Tenants;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Tenants.GetPublicBySlug;
using Endatix.Framework.FeatureFlags;
using System.Text.Json;
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
    private readonly IFeatureGate _featureGate = Substitute.For<IFeatureGate>();

    [Fact]
    public async Task ExecuteAsync_UnknownSlug_ReturnsNotFound()
    {
        // Arrange
        var endpoint = Factory.Create<GetPublicTenantEndpoint>(_mediator, _cache, _featureGate);
        _mediator.Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PublicTenantDto>.NotFound("Tenant not found."));

        // Act
        var response = await endpoint.ExecuteAsync(
            new GetPublicTenantRequest { Slug = "xK9mP2qR" },
            TestContext.Current.CancellationToken);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData("acme")]
    [InlineData("acme-regional-surveys")]
    [InlineData("xk9mp2qr8")]
    public async Task ExecuteAsync_InvalidShortUrl_ReturnsNotFoundWithoutQuery(string slug)
    {
        var endpoint = Factory.Create<GetPublicTenantEndpoint>(_mediator, _cache, _featureGate);

        var response = await endpoint.ExecuteAsync(
            new GetPublicTenantRequest { Slug = slug },
            TestContext.Current.CancellationToken);

        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await _mediator.DidNotReceive().Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsDtoWithoutNumericId()
    {
        // Arrange
        var endpoint = Factory.Create<GetPublicTenantEndpoint>(_mediator, _cache, _featureGate);
        _mediator.Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PublicTenantDto(
                "xk9mp2qr",
                "Acme",
                true,
                ["endatix"])));

        // Act
        var response = await endpoint.ExecuteAsync(
            new GetPublicTenantRequest { Slug = "xK9mP2qR" },
            TestContext.Current.CancellationToken);

        // Assert
        var ok = response.Result.As<Ok<PublicTenantModel>>();
        ok.Value!.Slug.Should().Be("xk9mp2qr");
        ok.Value.SelfRegistrationEnabled.Should().BeTrue();
        ok.Value.GetType().GetProperty("Id").Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Success_CachesAndSkipsSecondLookup()
    {
        // Arrange
        var endpoint = Factory.Create<GetPublicTenantEndpoint>(_mediator, _cache, _featureGate);
        _mediator.Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PublicTenantDto("xk9mp2qr", "Acme", true, ["endatix"])));
        var request = new GetPublicTenantRequest { Slug = "xk9mp2qr" };

        // Act
        await endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);
        await endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await _mediator.Received(1).Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Success_MixedCaseSlugSharesCacheKey()
    {
        // Arrange
        var endpoint = Factory.Create<GetPublicTenantEndpoint>(_mediator, _cache, _featureGate);
        _mediator.Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PublicTenantDto("xk9mp2qr", "Acme", true, ["endatix"])));

        // Act
        await endpoint.ExecuteAsync(
            new GetPublicTenantRequest { Slug = "xK9mP2qR" },
            TestContext.Current.CancellationToken);
        await endpoint.ExecuteAsync(
            new GetPublicTenantRequest { Slug = "xk9mp2qr" },
            TestContext.Current.CancellationToken);

        // Assert
        await _mediator.Received(1).Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NotFound_DoesNotCache()
    {
        // Arrange
        var endpoint = Factory.Create<GetPublicTenantEndpoint>(_mediator, _cache, _featureGate);
        _mediator.Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PublicTenantDto>.NotFound("Tenant not found."));
        var request = new GetPublicTenantRequest { Slug = "xk9mp2qr" };

        // Act
        await endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);
        await endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await _mediator.Received(2).Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhiteLabelingOn_ReturnsThePublishedBrandAsAnObject()
    {
        // Arrange
        var endpoint = Factory.Create<GetPublicTenantEndpoint>(_mediator, _cache, _featureGate);
        _featureGate.IsEnabledAsync(FeatureFlags.WhiteLabeling, Arg.Any<CancellationToken>()).Returns(true);
        GivenPublishedBrand();

        // Act
        var response = await endpoint.ExecuteAsync(
            new GetPublicTenantRequest { Slug = "xk9mp2qr" },
            TestContext.Current.CancellationToken);

        // Assert
        var branding = response.Result.As<Ok<PublicTenantModel>>().Value!.Branding;
        branding!.Value.ValueKind.Should().Be(JsonValueKind.Object);
        branding.Value.GetProperty("version").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhiteLabelingOff_ReturnsNoBrandEvenWhenCached()
    {
        // Arrange
        var endpoint = Factory.Create<GetPublicTenantEndpoint>(_mediator, _cache, _featureGate);
        _featureGate.IsEnabledAsync(FeatureFlags.WhiteLabeling, Arg.Any<CancellationToken>()).Returns(true, false);
        GivenPublishedBrand();
        var request = new GetPublicTenantRequest { Slug = "xk9mp2qr" };
        await endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Act
        var response = await endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.Result.As<Ok<PublicTenantModel>>().Value!.Branding.Should().BeNull();
    }

    private void GivenPublishedBrand() =>
        _mediator.Send(Arg.Any<GetPublicTenantQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PublicTenantDto("xk9mp2qr", "Acme", true, ["endatix"], """{"version":1}""")));

    private static HybridCache CreateCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
}

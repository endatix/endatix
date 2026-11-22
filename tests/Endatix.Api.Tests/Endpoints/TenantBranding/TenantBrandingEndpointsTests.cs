using System.Text.Json;
using Endatix.Api.Endpoints.TenantBranding;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantBranding;
using Endatix.Core.UseCases.TenantBranding.Get;
using Endatix.Core.UseCases.TenantBranding.Publishing;
using Endatix.Core.UseCases.TenantBranding.Restoring;
using Endatix.Core.UseCases.Tenants;
using Endatix.Core.UseCases.Tenants.GetById;
using Endatix.Infrastructure.Caching;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Api.Tests.Endpoints.TenantBranding;

public sealed class TenantBrandingEndpointsTests
{
    private const long TENANT_ID = 42;
    private const string SHORT_URL = "xk9mp2qr";
    private const string BLUE = """{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#0052d1"}}}}""";

    private static readonly DateTime _publishedAt = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly HybridCache _cache = CreateCache();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IUserContext _userContext = Substitute.For<IUserContext>();

    public TenantBrandingEndpointsTests()
    {
        _tenantContext.TenantId.Returns(TENANT_ID);
        _userContext.GetCurrentUserId().Returns("7");
        _mediator.Send(Arg.Is<GetTenantByIdQuery>(query => query.TenantId == TENANT_ID), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new TenantDto { Id = TENANT_ID, Name = "Acme", ShortUrl = SHORT_URL }));
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_ReturnsThePublishedBrandAsAnObjectWithItsETag()
    {
        // Arrange
        var endpoint = Factory.Create<GetTenantBranding>(_mediator);
        _mediator.Send(Arg.Any<GetTenantBrandingQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new TenantBrandingReadDto(true, new TenantBrandingDto(BLUE, 1, 1, _publishedAt))));

        // Act
        var response = await endpoint.ExecuteAsync(Cancellation);

        // Assert
        var model = response.Result.As<Ok<TenantBrandingModel>>().Value!;
        model.Entitled.Should().BeTrue();
        model.Published!.Value.ValueKind.Should().Be(JsonValueKind.Object);
        model.Published.Value.GetProperty("tokens").GetProperty("color").GetProperty("primary")
            .GetProperty("$value").GetString().Should().Be("#0052d1");
        model.PublishedNumber.Should().Be(1);
        model.Version.Should().Be(1);
        endpoint.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"1\"");
    }

    [Fact]
    public async Task Publish_WithoutIfMatch_IsPreconditionRequiredAndSendsNothing()
    {
        // Arrange
        var endpoint = CreateWrite<PublishTenantBranding>();

        // Act
        var response = await endpoint.ExecuteAsync(new PublishTenantBrandingRequest { Override = Json(BLUE) }, Cancellation);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status428PreconditionRequired);
        await _mediator.DidNotReceive().Send(Arg.Any<PublishTenantBrandingCommand>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("W/\"1\"")]
    [InlineData("*")]
    [InlineData("\"one\"")]
    public async Task Publish_WithAnUnusableIfMatch_IsBadRequest(string ifMatch)
    {
        // Arrange
        var endpoint = CreateWrite<PublishTenantBranding>();

        // Act
        var response = await endpoint.ExecuteAsync(
            new PublishTenantBrandingRequest { IfMatch = ifMatch, Override = Json(BLUE) },
            Cancellation);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        await _mediator.DidNotReceive().Send(Arg.Any<PublishTenantBrandingCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_SendsTheVersionTheBodyAndTheCaller_AndEvictsThePublicRead()
    {
        // Arrange
        var endpoint = CreateWrite<PublishTenantBranding>();
        _mediator.Send(Arg.Any<PublishTenantBrandingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new TenantBrandingDto(BLUE, 4, 4, _publishedAt)));
        await PrimePublicReadAsync();

        // Act
        var response = await endpoint.ExecuteAsync(
            new PublishTenantBrandingRequest { IfMatch = "\"3\"", Override = Json(BLUE) },
            Cancellation);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<PublishTenantBrandingCommand>(command =>
                command.ExpectedVersion == 3 && command.UserId == 7 && command.OverrideJson.Contains("#0052d1")),
            Arg.Any<CancellationToken>());
        response.Result.As<Ok<TenantBrandingModel>>().Value!.Version.Should().Be(4);
        endpoint.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"4\"");
        (await PublicReadIsCachedAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Publish_StaleVersion_IsPreconditionFailed()
    {
        // Arrange
        var endpoint = CreateWrite<PublishTenantBranding>();
        _mediator.Send(Arg.Any<PublishTenantBrandingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantBrandingDto>.Conflict("The branding changed since it was read; it is now at version 5."));

        // Act
        var response = await endpoint.ExecuteAsync(
            new PublishTenantBrandingRequest { IfMatch = "\"3\"", Override = Json(BLUE) },
            Cancellation);

        // Assert
        var problem = response.Result.As<ProblemHttpResult>();
        problem.StatusCode.Should().Be(StatusCodes.Status412PreconditionFailed);
        problem.ProblemDetails.Detail.Should().Contain("version 5");
    }

    [Fact]
    public async Task Publish_InvalidOverride_ListsEachProblemWithItsPathAndCode()
    {
        // Arrange
        var endpoint = CreateWrite<PublishTenantBranding>();
        _mediator.Send(Arg.Any<PublishTenantBrandingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantBrandingDto>.Invalid(
                new ValidationError("/tokens/color/primary", "Too little contrast.", "low_contrast", ValidationSeverity.Error),
                new ValidationError("/extra", "Unknown field.", "unknown_field", ValidationSeverity.Error)));

        // Act
        var response = await endpoint.ExecuteAsync(
            new PublishTenantBrandingRequest { IfMatch = "\"0\"", Override = Json(BLUE) },
            Cancellation);

        // Assert
        var problem = response.Result.As<ProblemHttpResult>();
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Extensions["problems"].Should().BeEquivalentTo(new[]
        {
            new BrandingProblem("/tokens/color/primary", "low_contrast", "Too little contrast."),
            new BrandingProblem("/extra", "unknown_field", "Unknown field."),
        });
    }

    [Fact]
    public async Task Publish_Refused_KeepsThePublicRead()
    {
        // Arrange
        var endpoint = CreateWrite<PublishTenantBranding>();
        _mediator.Send(Arg.Any<PublishTenantBrandingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantBrandingDto>.Forbidden("The tenant's plan does not include branding."));
        await PrimePublicReadAsync();

        // Act
        var response = await endpoint.ExecuteAsync(
            new PublishTenantBrandingRequest { IfMatch = "\"0\"", Override = Json(BLUE) },
            Cancellation);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await PublicReadIsCachedAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Restore_NamesTheNumberAndTheVersion_AndEvictsThePublicRead()
    {
        // Arrange
        var endpoint = CreateWrite<RestoreTenantBranding>();
        _mediator.Send(Arg.Any<RestoreTenantBrandingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new TenantBrandingDto(BLUE, 3, 3, _publishedAt)));
        await PrimePublicReadAsync();

        // Act
        var response = await endpoint.ExecuteAsync(
            new RestoreTenantBrandingRequest { IfMatch = "2", Number = 1 },
            Cancellation);

        // Assert
        await _mediator.Received(1).Send(new RestoreTenantBrandingCommand(1, 2, 7), Arg.Any<CancellationToken>());
        response.Result.As<Ok<TenantBrandingModel>>().Value!.PublishedNumber.Should().Be(3);
        (await PublicReadIsCachedAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Restore_UnknownNumber_IsBadRequestWithTheDomainCode()
    {
        // Arrange
        var endpoint = CreateWrite<RestoreTenantBranding>();
        _mediator.Send(Arg.Any<RestoreTenantBrandingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantBrandingDto>.Invalid(
                new ValidationError("", "There is no branding version 9.", "unknown_version", ValidationSeverity.Error)));

        // Act
        var response = await endpoint.ExecuteAsync(
            new RestoreTenantBrandingRequest { IfMatch = "\"2\"", Number = 9 },
            Cancellation);

        // Assert
        var problem = response.Result.As<ProblemHttpResult>();
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Extensions["problems"].Should().BeEquivalentTo(new[]
        {
            new BrandingProblem("", "unknown_version", "There is no branding version 9."),
        });
    }

    [Fact]
    public async Task Rollback_SendsTheVersion()
    {
        // Arrange
        var endpoint = CreateWrite<RollbackTenantBranding>();
        _mediator.Send(Arg.Any<RollbackTenantBrandingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new TenantBrandingDto(BLUE, 3, 3, _publishedAt)));

        // Act
        var response = await endpoint.ExecuteAsync(new RollbackTenantBrandingRequest { IfMatch = "\"2\"" }, Cancellation);

        // Assert
        await _mediator.Received(1).Send(new RollbackTenantBrandingCommand(2, 7), Arg.Any<CancellationToken>());
        response.Result.As<Ok<TenantBrandingModel>>().Value!.Version.Should().Be(3);
    }

    private TEndpoint CreateWrite<TEndpoint>() where TEndpoint : class, IEndpoint =>
        Factory.Create<TEndpoint>(_mediator, _cache, _tenantContext, _userContext);

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private Task PrimePublicReadAsync() =>
        _cache.SetAsync(PublicTenantCacheKeys.Entry(SHORT_URL), "cached", cancellationToken: Cancellation).AsTask();

    private async Task<bool> PublicReadIsCachedAsync() =>
        await _cache.GetOrCreateAsync(
            PublicTenantCacheKeys.Entry(SHORT_URL),
            _ => ValueTask.FromResult("fresh"),
            cancellationToken: Cancellation) == "cached";

    private static HybridCache CreateCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
}

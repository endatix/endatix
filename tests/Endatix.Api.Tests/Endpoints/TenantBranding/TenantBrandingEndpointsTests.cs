using System.Text.Json;
using Endatix.Api.Endpoints.TenantBranding;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.TenantBranding;
using Endatix.Core.UseCases.TenantBranding.Get;
using Endatix.Core.UseCases.TenantBranding.Publishing;
using Endatix.Core.UseCases.TenantBranding.Restoring;
using Endatix.Infrastructure.Caching;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.TenantBranding;

public sealed class TenantBrandingEndpointsTests
{
    private const string BLUE = """{"version":1,"tokens":{"color":{"primary":{"$type":"color","$value":"#0052d1"}}}}""";

    private static readonly DateTime _publishedAt = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IPublicTenantCacheInvalidator _publicTenantCache = Substitute.For<IPublicTenantCacheInvalidator>();
    private readonly IUserContext _userContext = Substitute.For<IUserContext>();

    public TenantBrandingEndpointsTests()
    {
        _userContext.GetCurrentUserId().Returns("7");
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
        var endpoint = CreateWrite<PublishTenantBranding>(ifMatch: null);

        // Act
        var response = await endpoint.ExecuteAsync(Body(BLUE), Cancellation);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status428PreconditionRequired);
        await _mediator.DidNotReceive().Send(Arg.Any<PublishTenantBrandingCommand>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("W/\"1\"")]
    [InlineData("*")]
    [InlineData("\"1\", \"2\"")]
    [InlineData("\"one\"")]
    public async Task Publish_WithAnUnusableIfMatch_IsBadRequest(string ifMatch)
    {
        // Arrange
        var endpoint = CreateWrite<PublishTenantBranding>(ifMatch);

        // Act
        var response = await endpoint.ExecuteAsync(Body(BLUE), Cancellation);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        await _mediator.DidNotReceive().Send(Arg.Any<PublishTenantBrandingCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_SendsTheBodyTheVersionAndTheCaller_AndEvictsThePublicRead()
    {
        // Arrange
        var endpoint = CreateWrite<PublishTenantBranding>("\"3\"");
        _mediator.Send(Arg.Any<PublishTenantBrandingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new TenantBrandingDto(BLUE, 4, 4, _publishedAt)));

        // Act
        var response = await endpoint.ExecuteAsync(Body(BLUE), Cancellation);

        // Assert
        await _mediator.Received(1).Send(new PublishTenantBrandingCommand(BLUE, 3, 7), Arg.Any<CancellationToken>());
        response.Result.As<Ok<TenantBrandingModel>>().Value!.Version.Should().Be(4);
        endpoint.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"4\"");
        await _publicTenantCache.Received(1).InvalidateCurrentTenantAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_OfTheLiveBrand_KeepsThePublicRead()
    {
        // Arrange
        var endpoint = CreateWrite<PublishTenantBranding>("\"4\"");
        _mediator.Send(Arg.Any<PublishTenantBrandingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new TenantBrandingDto(BLUE, 4, 4, _publishedAt)));

        // Act
        var response = await endpoint.ExecuteAsync(Body(BLUE), Cancellation);

        // Assert
        response.Result.As<Ok<TenantBrandingModel>>().Value!.Version.Should().Be(4);
        await _publicTenantCache.DidNotReceive().InvalidateCurrentTenantAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_StaleVersion_IsPreconditionFailedWithTheCurrentVersion()
    {
        // Arrange
        var endpoint = CreateWrite<PublishTenantBranding>("\"3\"");
        _mediator.Send(Arg.Any<PublishTenantBrandingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantBrandingDto>.Conflict("The branding changed since it was read; it is now at version 5."));
        _mediator.Send(Arg.Any<GetTenantBrandingQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new TenantBrandingReadDto(true, new TenantBrandingDto(BLUE, 5, 5, _publishedAt))));

        // Act
        var response = await endpoint.ExecuteAsync(Body(BLUE), Cancellation);

        // Assert
        var problem = response.Result.As<ProblemHttpResult>();
        problem.StatusCode.Should().Be(StatusCodes.Status412PreconditionFailed);
        problem.ProblemDetails.Extensions["version"].Should().Be("5");
        endpoint.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"5\"");
        await _publicTenantCache.DidNotReceive().InvalidateCurrentTenantAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_InvalidOverride_ListsEachProblemWithItsPathAndCode()
    {
        // Arrange
        var endpoint = CreateWrite<PublishTenantBranding>("\"0\"");
        _mediator.Send(Arg.Any<PublishTenantBrandingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantBrandingDto>.Invalid(
                new ValidationError("/tokens/color/primary", "Too little contrast.", "low_contrast", ValidationSeverity.Error),
                new ValidationError("", "Is not valid JSON.", "invalid_json", ValidationSeverity.Error)));

        // Act
        var response = await endpoint.ExecuteAsync(Body("{"), Cancellation);

        // Assert
        var problem = response.Result.As<ProblemHttpResult>();
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Extensions["problems"].Should().BeEquivalentTo(new[]
        {
            new BrandingProblem("/tokens/color/primary", "low_contrast", "Too little contrast."),
            new BrandingProblem("", "invalid_json", "Is not valid JSON."),
        });
    }

    [Fact]
    public async Task Publish_Refused_KeepsThePublicRead()
    {
        // Arrange
        var endpoint = CreateWrite<PublishTenantBranding>("\"0\"");
        _mediator.Send(Arg.Any<PublishTenantBrandingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantBrandingDto>.Forbidden("The tenant's plan does not include branding."));

        // Act
        var response = await endpoint.ExecuteAsync(Body(BLUE), Cancellation);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        await _publicTenantCache.DidNotReceive().InvalidateCurrentTenantAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restore_NamesTheNumberAndTheVersion_AndEvictsThePublicRead()
    {
        // Arrange
        var endpoint = CreateWrite<RestoreTenantBranding>("2");
        _mediator.Send(Arg.Any<RestoreTenantBrandingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new TenantBrandingDto(BLUE, 3, 3, _publishedAt)));

        // Act
        var response = await endpoint.ExecuteAsync(new RestoreTenantBrandingRequest { Number = 1 }, Cancellation);

        // Assert
        await _mediator.Received(1).Send(new RestoreTenantBrandingCommand(1, 2, 7), Arg.Any<CancellationToken>());
        response.Result.As<Ok<TenantBrandingModel>>().Value!.PublishedNumber.Should().Be(3);
        await _publicTenantCache.Received(1).InvalidateCurrentTenantAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restore_UnknownNumber_IsBadRequestWithTheDomainCode()
    {
        // Arrange
        var endpoint = CreateWrite<RestoreTenantBranding>("\"2\"");
        _mediator.Send(Arg.Any<RestoreTenantBrandingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantBrandingDto>.Invalid(
                new ValidationError("", "There is no branding version 9.", "unknown_version", ValidationSeverity.Error)));

        // Act
        var response = await endpoint.ExecuteAsync(new RestoreTenantBrandingRequest { Number = 9 }, Cancellation);

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
        var endpoint = CreateWrite<RollbackTenantBranding>("\"2\"");
        _mediator.Send(Arg.Any<RollbackTenantBrandingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new TenantBrandingDto(BLUE, 3, 3, _publishedAt)));

        // Act
        var response = await endpoint.ExecuteAsync(EmptyRequest.Instance, Cancellation);

        // Assert
        await _mediator.Received(1).Send(new RollbackTenantBrandingCommand(2, 7), Arg.Any<CancellationToken>());
        response.Result.As<Ok<TenantBrandingModel>>().Value!.Version.Should().Be(3);
    }

    private TEndpoint CreateWrite<TEndpoint>(string? ifMatch) where TEndpoint : class, IEndpoint =>
        Factory.Create<TEndpoint>(
            context =>
            {
                if (ifMatch is not null)
                {
                    context.Request.Headers.IfMatch = ifMatch;
                }
            },
            _mediator,
            _publicTenantCache,
            _userContext);

    private static PublishTenantBrandingRequest Body(string json) => new() { Content = json };
}

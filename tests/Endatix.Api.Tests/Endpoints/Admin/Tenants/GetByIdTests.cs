using Endatix.Api.Endpoints.Admin.Tenants;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Tenants;
using Endatix.Core.UseCases.Tenants.GetById;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using GetTenantEndpoint = Endatix.Api.Endpoints.Admin.Tenants.GetById;

namespace Endatix.Api.Tests.Endpoints.Admin.Tenants;

public sealed class GetByIdTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    [Fact]
    public async Task ExecuteAsync_MultiTenancyDisabled_ReturnsNotFoundWithoutSendingQuery()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: false);

        // Act
        var response = await endpoint.ExecuteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await _mediator.DidNotReceive().Send(Arg.Any<GetTenantByIdQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkTenant()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<GetTenantByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(SampleTenant()));

        // Act
        var response = await endpoint.ExecuteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result.As<Ok<TenantModel>>();
        okResult.Value!.ShortUrl.Should().Be("xk9mp2qr");
        okResult.Value.AllowSelfRegistration.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_MapsRequestToQuery()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<GetTenantByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(SampleTenant()));

        // Act
        await endpoint.ExecuteAsync(ValidRequest(), CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GetTenantByIdQuery>(query => query.TenantId == 42),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_TenantNotFound_ReturnsNotFoundProblem()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<GetTenantByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantDto>.NotFound("Tenant not found."));

        // Act
        var response = await endpoint.ExecuteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    private GetTenantEndpoint CreateEndpoint(bool multiTenancyEnabled) =>
        Factory.Create<GetTenantEndpoint>(_mediator, MultiTenancyConfiguration.Create(multiTenancyEnabled));

    private static GetTenantByIdRequest ValidRequest() => new() { TenantId = 42 };

    private static TenantDto SampleTenant() => new()
    {
        Id = 42,
        Name = "Acme",
        ShortUrl = "xk9mp2qr",
        AllowSelfRegistration = true,
        AllowedAuthProviderKeys = ["google"],
        DefaultRegistrationRoleName = "Respondent",
        CreatedAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
    };
}

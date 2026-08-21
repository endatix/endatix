using Endatix.Api.Endpoints.Admin.Tenants;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Tenants;
using Endatix.Core.UseCases.Tenants.Update;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using UpdateTenantEndpoint = Endatix.Api.Endpoints.Admin.Tenants.Update;

namespace Endatix.Api.Tests.Endpoints.Admin.Tenants;

public sealed class UpdateTests
{
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
        await _mediator.DidNotReceive().Send(Arg.Any<UpdateTenantCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithUpdatedTenant()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<UpdateTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(SampleTenant()));

        // Act
        var response = await endpoint.ExecuteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result.As<Ok<TenantModel>>();
        okResult.Value!.Name.Should().Be("Renamed");
        okResult.Value.Slug.Should().Be("acme");
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_MapsRequestToCommand()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<UpdateTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(SampleTenant()));

        // Act
        await endpoint.ExecuteAsync(ValidRequest(), CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<UpdateTenantCommand>(command =>
                command.TenantId == 42
                && command.Name == "Renamed"
                && command.AllowSelfRegistration == true
                && command.DefaultRegistrationRoleName == "Respondent"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_TenantNotFound_ReturnsNotFoundProblem()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<UpdateTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantDto>.NotFound("Tenant not found."));

        // Act
        var response = await endpoint.ExecuteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ForbiddenRegistrationRole_ReturnsBadRequestProblem()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<UpdateTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantDto>.Invalid(new ValidationError("Role is not allowed.")));

        // Act
        var response = await endpoint.ExecuteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private UpdateTenantEndpoint CreateEndpoint(bool multiTenancyEnabled) =>
        Factory.Create<UpdateTenantEndpoint>(_mediator, MultiTenancyConfiguration.Create(multiTenancyEnabled));

    private static UpdateTenantRequest ValidRequest() => new()
    {
        TenantId = 42,
        Name = "Renamed",
        AllowSelfRegistration = true,
        DefaultRegistrationRoleName = "Respondent"
    };

    private static TenantDto SampleTenant() => new()
    {
        Id = 42,
        Name = "Renamed",
        Slug = "acme",
        AllowSelfRegistration = true,
        AllowedAuthProviderKeys = ["google"],
        DefaultRegistrationRoleName = "Respondent",
        CreatedAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        ModifiedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
    };
}

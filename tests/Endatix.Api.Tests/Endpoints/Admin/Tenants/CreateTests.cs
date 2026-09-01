using Endatix.Api.Endpoints.Admin.Tenants;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Tenants;
using Endatix.Core.UseCases.Tenants.Create;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using CreateTenantEndpoint = Endatix.Api.Endpoints.Admin.Tenants.Create;

namespace Endatix.Api.Tests.Endpoints.Admin.Tenants;

public sealed class CreateTests
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
        await _mediator.DidNotReceive().Send(Arg.Any<CreateTenantCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsCreatedTenant()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<CreateTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantDto>.Created(SampleTenant()));

        // Act
        var response = await endpoint.ExecuteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        var createdResult = response.Result.As<Created<TenantModel>>();
        createdResult.Value!.Id.Should().Be(42);
        createdResult.Value.ShortUrl.Should().Be("xk9mp2qr");
        createdResult.Value.AllowSelfRegistration.Should().BeTrue();
        createdResult.Value.AllowedAuthProviderKeys.Should().BeEquivalentTo(["google"]);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_DoesNotSendClientShortUrl()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<CreateTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantDto>.Created(SampleTenant()));

        // Act
        await endpoint.ExecuteAsync(ValidRequest(), CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateTenantCommand>(command =>
                command.Name == "Acme"
                && command.Description == "Primary tenant"
                && command.AllowSelfRegistration
                && command.DefaultRegistrationRoleName == "Respondent"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_InvalidCommand_ReturnsBadRequestProblem()
    {
        // Arrange
        var endpoint = CreateEndpoint(multiTenancyEnabled: true);
        _mediator.Send(Arg.Any<CreateTenantCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantDto>.Invalid(new ValidationError("Name is required.")));

        // Act
        var response = await endpoint.ExecuteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        response.Result.As<ProblemHttpResult>().StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private CreateTenantEndpoint CreateEndpoint(bool multiTenancyEnabled) =>
        Factory.Create<CreateTenantEndpoint>(_mediator, MultiTenancyConfiguration.Create(multiTenancyEnabled));

    private static CreateTenantRequest ValidRequest() => new()
    {
        Name = "Acme",
        Description = "Primary tenant",
        AllowSelfRegistration = true,
        AllowedAuthProviderKeys = ["google"],
        DefaultRegistrationRoleName = "Respondent"
    };

    private static TenantDto SampleTenant() => new()
    {
        Id = 42,
        Name = "Acme",
        ShortUrl = "xk9mp2qr",
        Description = "Primary tenant",
        AllowSelfRegistration = true,
        AllowedAuthProviderKeys = ["google"],
        DefaultRegistrationRoleName = "Respondent",
        CreatedAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
    };
}

/// <summary>
/// Builds the minimal configuration the tenant endpoints read to resolve the deployment-scoped
/// multi-tenancy flag.
/// </summary>
internal static class MultiTenancyConfiguration
{
    internal static IConfiguration Create(bool enabled) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Endatix:FeatureFlags:MultiTenancy"] = enabled ? "true" : "false"
        })
        .Build();
}

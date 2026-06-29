using Endatix.Api.Endpoints.Admin.Auth;
using Endatix.Core.Features.Auth;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Admin.Auth.GetSettings;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Admin.Auth;

public class GetAuthSettingsTests
{
    private readonly IMediator _mediator;
    private readonly GetAuthSettings _endpoint;

    public GetAuthSettingsTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<GetAuthSettings>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMediatorReturnsSuccess_ReturnsOkWithSettings()
    {
        // Arrange
        var settings = new AuthSettingsDto
        {
            PlatformAdminRequiresLocalApproval = true,
            ConfigurationErrors = [],
            Providers = []
        };

        _mediator
            .Send(Arg.Any<GetAuthSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(settings));

        // Act
        var response = await _endpoint.ExecuteAsync(CancellationToken.None);

        // Assert
        var okResult = response.Result.As<Ok<AuthSettingsDto>>();
        okResult.Value.Should().NotBeNull();
        okResult.Value.Should().Be(settings);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMediatorReturnsInvalid_ReturnsProblemHttpResult()
    {
        // Arrange
        _mediator
            .Send(Arg.Any<GetAuthSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Invalid(new ValidationError("Invalid settings")));

        // Act
        var response = await _endpoint.ExecuteAsync(CancellationToken.None);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}

using Endatix.Api.Endpoints.Auth;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Auth;

public sealed class GetInviteDetailsTests
{
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly GetInviteDetails _endpoint;

    public GetInviteDetailsTests()
    {
        _emailVerificationService = Substitute.For<IEmailVerificationService>();
        _endpoint = Factory.Create<GetInviteDetails>(_emailVerificationService);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInvite_ReturnsInviteEmail()
    {
        // Arrange
        var request = new GetInviteDetailsRequest { Token = "valid-token" };
        var user = new User(123L, "user@example.com", "user@example.com", isVerified: false);

        _emailVerificationService
            .GetPendingInviteUserAsync(request.Token, Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));

        // Act
        var response = await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        var okResult = response.Result.As<Ok<GetInviteDetailsResponse>>();
        okResult.Value!.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task ExecuteAsync_WithExpiredInvite_ReturnsProblemResult()
    {
        // Arrange
        var request = new GetInviteDetailsRequest { Token = "expired-token" };

        _emailVerificationService
            .GetPendingInviteUserAsync(request.Token, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Invalid(new ValidationError("Invite token has expired")));

        // Act
        var response = await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(400);
        problemResult.ProblemDetails.Detail.Should().Contain("Invite token has expired");
    }
}

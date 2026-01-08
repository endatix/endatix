using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.CreateAccessToken;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class CreateAccessTokenTests
{
    private readonly IMediator _mediator;
    private readonly CreateAccessToken _endpoint;

    public CreateAccessTokenTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<CreateAccessToken>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithToken()
    {
        // Arrange
        var request = new CreateAccessTokenRequest
        {
            FormId = 123,
            SubmissionId = 456,
            ExpiryMinutes = 60,
            Permissions = new List<string> { "view", "edit" }
        };

        var tokenDto = new SubmissionAccessTokenDto(
            "12345.1234567890.rw.signature",
            DateTime.UtcNow.AddMinutes(60),
            new[] { "view", "edit" });
        var result = Result.Success(tokenDto);

        _mediator.Send(Arg.Any<CreateAccessTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<CreateAccessTokenResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Token.Should().Be(tokenDto.Token);
        okResult!.Value!.ExpiresAt.Should().Be(tokenDto.ExpiresAt);
        okResult!.Value!.Permissions.Should().BeEquivalentTo(tokenDto.Permissions);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateAccessTokenRequest
        {
            FormId = 123,
            SubmissionId = 456,
            ExpiryMinutes = 60,
            Permissions = new List<string> { "invalid" }
        };

        var result = Result<SubmissionAccessTokenDto>.Invalid(new ValidationError { ErrorMessage = "Invalid permission" });

        _mediator.Send(Arg.Any<CreateAccessTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_SubmissionNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new CreateAccessTokenRequest
        {
            FormId = 123,
            SubmissionId = 999,
            ExpiryMinutes = 60,
            Permissions = new List<string> { "view" }
        };

        var result = Result<SubmissionAccessTokenDto>.NotFound("Submission not found");

        _mediator.Send(Arg.Any<CreateAccessTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_UnauthorizedUser_ReturnsForbidden()
    {
        // Arrange
        var request = new CreateAccessTokenRequest
        {
            FormId = 123,
            SubmissionId = 456,
            ExpiryMinutes = 60,
            Permissions = new List<string> { "view" }
        };

        var result = Result<SubmissionAccessTokenDto>.Forbidden("Access denied");

        _mediator.Send(Arg.Any<CreateAccessTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new CreateAccessTokenRequest
        {
            FormId = 123,
            SubmissionId = 456,
            ExpiryMinutes = 1440,
            Permissions = new List<string> { "view", "edit", "export" }
        };

        var tokenDto = new SubmissionAccessTokenDto("token", DateTime.UtcNow.AddMinutes(1440), new[] { "view", "edit", "export" });
        var result = Result.Success(tokenDto);

        _mediator.Send(Arg.Any<CreateAccessTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateAccessTokenCommand>(cmd =>
                cmd.FormId == request.FormId &&
                cmd.SubmissionId == request.SubmissionId &&
                cmd.ExpiryMinutes == request.ExpiryMinutes &&
                cmd.Permissions.SequenceEqual(request.Permissions!)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_AllPermissions_ReturnsCorrectToken()
    {
        // Arrange
        var request = new CreateAccessTokenRequest
        {
            FormId = 123,
            SubmissionId = 456,
            ExpiryMinutes = 120,
            Permissions = new List<string> { "view", "edit", "export" }
        };

        var tokenDto = new SubmissionAccessTokenDto(
            "12345.1234567890.rwx.signature",
            DateTime.UtcNow.AddMinutes(120),
            new[] { "view", "edit", "export" });
        var result = Result.Success(tokenDto);

        _mediator.Send(Arg.Any<CreateAccessTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<CreateAccessTokenResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value!.Permissions.Should().HaveCount(3);
        okResult!.Value!.Permissions.Should().Contain("view");
        okResult!.Value!.Permissions.Should().Contain("edit");
        okResult!.Value!.Permissions.Should().Contain("export");
    }

    [Fact]
    public async Task ExecuteAsync_ShortExpiry_ReturnsCorrectExpirationTime()
    {
        // Arrange
        var request = new CreateAccessTokenRequest
        {
            FormId = 123,
            SubmissionId = 456,
            ExpiryMinutes = 5,
            Permissions = new List<string> { "view" }
        };

        var expiresAt = DateTime.UtcNow.AddMinutes(5);
        var tokenDto = new SubmissionAccessTokenDto("token", expiresAt, new[] { "view" });
        var result = Result.Success(tokenDto);

        _mediator.Send(Arg.Any<CreateAccessTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<CreateAccessTokenResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value!.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }
}

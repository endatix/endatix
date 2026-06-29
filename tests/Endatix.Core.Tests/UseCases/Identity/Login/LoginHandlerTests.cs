using MediatR;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.Login;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Core.Tests.UseCases.Identity.Login;

public class LoginHandlerTests
{
    private readonly IAuthService _authService;
    private readonly IUserTokenService _tokenService;
    private readonly ICurrentUserAuthorizationService _authorizationService;
    private readonly IMediator _mediator;
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _authService = Substitute.For<IAuthService>();
        _tokenService = Substitute.For<IUserTokenService>();
        _mediator = Substitute.For<IMediator>();
        _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
        _handler = new LoginHandler(_authService, _tokenService, _authorizationService, _mediator);
    }

    [Fact]
    public async Task Handle_InvalidCredentials_ReturnsInvalidResult()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "password");
        var validationErrors = new List<ValidationError> { new("Invalid credentials") };
        _authService.ValidateCredentials(command.Email, command.Password, Arg.Any<CancellationToken>())
            .Returns(Result.Invalid(validationErrors));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().BeEquivalentTo(validationErrors);
        _tokenService.DidNotReceive().IssueRefreshToken();
    }

    [Fact]
    public async Task Handle_AuthServiceError_ReturnsErrorResult()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "password");
        _authService.ValidateCredentials(command.Email, command.Password, Arg.Any<CancellationToken>())
            .Returns(Result.Error());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError().Should().BeTrue();
        _tokenService.DidNotReceive().IssueRefreshToken();
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsSuccessResultWithTokens()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "password");
        var user = new User(1, SampleData.TENANT_ID, "testuser", "test@example.com", true);
        var accessToken = new TokenDto("access_token", DateTime.UtcNow.AddMinutes(15));
        var refreshToken = new TokenDto("refresh_token", DateTime.UtcNow.AddDays(7));

        _authService.ValidateCredentials(command.Email, command.Password, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(user));
        _tokenService.IssueRefreshToken().Returns(refreshToken);
        _authService.PersistLoginSessionAsync(
                user.Id,
                refreshToken.Token,
                refreshToken.ExpireAt,
                Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _tokenService.IssueAccessToken(user).Returns(accessToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().Be(accessToken);
        result.Value.RefreshToken.Should().Be(refreshToken);

        await _authService.Received(1).ValidateCredentials(
            command.Email,
            command.Password,
            Arg.Any<CancellationToken>());
        await _authService.Received(1).PersistLoginSessionAsync(
            user.Id,
            refreshToken.Token,
            refreshToken.ExpireAt,
            Arg.Any<CancellationToken>());
        await _authorizationService.Received(1).InvalidateAuthorizationDataCacheAsync(
            user.Id.ToString(),
            user.TenantId,
            Arg.Any<CancellationToken>());
        await _mediator.Received(1).Publish(
            Arg.Is<UserLoggedInEvent>(@event => @event.User == user),
            Arg.Any<CancellationToken>());
        await _authService.DidNotReceive().StoreRefreshToken(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PersistSessionFails_DoesNotIssueAccessToken()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "password");
        var user = new User(1, SampleData.TENANT_ID, "testuser", "test@example.com", true);
        var refreshToken = new TokenDto("refresh_token", DateTime.UtcNow.AddDays(7));

        _authService.ValidateCredentials(command.Email, command.Password, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(user));
        _tokenService.IssueRefreshToken().Returns(refreshToken);
        _authService.PersistLoginSessionAsync(
                user.Id,
                refreshToken.Token,
                refreshToken.ExpireAt,
                Arg.Any<CancellationToken>())
            .Returns(Result.Error());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError().Should().BeTrue();
        _tokenService.DidNotReceive().IssueAccessToken(Arg.Any<User>());
        await _mediator.DidNotReceive().Publish(Arg.Any<UserLoggedInEvent>(), Arg.Any<CancellationToken>());
    }
}

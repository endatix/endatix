using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Tests;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.RefreshToken;

namespace Endatix.Core.Tests.UseCases.Identity.RefreshToken;

public class RefreshTokenHandlerTests
{
    private readonly IAuthService _authService;
    private readonly IUserTokenService _tokenService;
    private readonly RefreshTokenHandler _handler;

    public RefreshTokenHandlerTests()
    {
        _authService = Substitute.For<IAuthService>();
        _tokenService = Substitute.For<IUserTokenService>();
        _handler = new RefreshTokenHandler(_authService, _tokenService);
    }

    [Fact]
    public async Task Handle_InvalidAccessToken_ReturnsInvalidResult()
    {
        var command = new RefreshTokenCommand("invalid_access_token", "refresh_token");
        var validationErrors = new List<ValidationError> { new("Invalid access token") };
        _tokenService.ReadAccessTokenSessionAsync(command.AccessToken, false)
            .Returns(Task.FromResult<Result<AccessTokenSession>>(Result.Invalid(validationErrors)));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().BeEquivalentTo(validationErrors);
    }

    [Fact]
    public async Task Handle_ExternalAccessTokenScheme_ReturnsInvalidResult()
    {
        var command = new RefreshTokenCommand("external_access_token", "refresh_token");
        var validationErrors = new List<ValidationError> { new("Token validation not supported for scheme: Keycloak.") };
        _tokenService.ReadAccessTokenSessionAsync(command.AccessToken, false)
            .Returns(Task.FromResult<Result<AccessTokenSession>>(Result.Invalid(validationErrors)));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().BeEquivalentTo(validationErrors);
    }

    [Fact]
    public async Task Handle_AccessTokenValidationError_ReturnsErrorResult()
    {
        var command = new RefreshTokenCommand("access_token", "refresh_token");
        _tokenService.ReadAccessTokenSessionAsync(command.AccessToken, false)
            .Returns(Task.FromResult<Result<AccessTokenSession>>(Result.Error()));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError().Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InvalidRefreshToken_ReturnsInvalidResult()
    {
        var command = new RefreshTokenCommand("access_token", "invalid_refresh_token");
        var userId = 1L;
        var validationErrors = new List<ValidationError> { new("Invalid refresh token") };

        _tokenService.ReadAccessTokenSessionAsync(command.AccessToken, false)
            .Returns(Task.FromResult(Result.Success(new AccessTokenSession(userId, SampleData.TENANT_ID, null))));
        _authService.ValidateRefreshToken(userId, command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(Result.Invalid(validationErrors));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().BeEquivalentTo(validationErrors);
    }

    [Fact]
    public async Task Handle_RefreshTokenValidationError_ReturnsErrorResult()
    {
        var command = new RefreshTokenCommand("access_token", "refresh_token");
        var userId = 1L;

        _tokenService.ReadAccessTokenSessionAsync(command.AccessToken, false)
            .Returns(Task.FromResult(Result.Success(new AccessTokenSession(userId, SampleData.TENANT_ID, null))));
        _authService.ValidateRefreshToken(userId, command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(Result.Error());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError().Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidTokens_ReturnsSuccessResultWithNewTokens()
    {
        var command = new RefreshTokenCommand("access_token", "refresh_token");
        var userId = 1L;
        var user = new User(userId, SampleData.TENANT_ID, "testuser", "test@example.com", true);
        var newAccessToken = new TokenDto("new_access_token", DateTime.UtcNow.AddMinutes(15));
        var newRefreshToken = new TokenDto("new_refresh_token", DateTime.UtcNow.AddDays(7));

        _tokenService.ReadAccessTokenSessionAsync(command.AccessToken, false)
            .Returns(Task.FromResult(Result.Success(new AccessTokenSession(userId, SampleData.TENANT_ID, null))));
        _authService.ValidateRefreshToken(userId, command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(user));
        _tokenService.IssueAccessToken(
                user,
                Arg.Is<AccessTokenIssueOptions>(options =>
                    options.TenantId == SampleData.TENANT_ID && options.ActorUserId == null))
            .Returns(newAccessToken);
        _tokenService.IssueRefreshToken().Returns(newRefreshToken);
        _authService.StoreRefreshToken(user.Id, newRefreshToken.Token, newRefreshToken.ExpireAt, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().Be(newAccessToken);
        result.Value.RefreshToken.Should().Be(newRefreshToken);

        await _authService.Received(1).StoreRefreshToken(user.Id, newRefreshToken.Token, newRefreshToken.ExpireAt, Arg.Any<CancellationToken>());
        _tokenService.DidNotReceive().IssueAccessToken(user);
    }

    [Fact]
    public async Task Handle_MembershipSession_RemintsSessionTenantNotUserHomeTenant()
    {
        var command = new RefreshTokenCommand("access_token", "refresh_token");
        var userId = 1L;
        const long sessionTenantId = 50;
        var user = new User(userId, SampleData.TENANT_ID, "testuser", "test@example.com", true);
        var newAccessToken = new TokenDto("switched_access_token", DateTime.UtcNow.AddMinutes(15));
        var newRefreshToken = new TokenDto("new_refresh_token", DateTime.UtcNow.AddDays(7));

        _tokenService.ReadAccessTokenSessionAsync(command.AccessToken, false)
            .Returns(Task.FromResult(Result.Success(new AccessTokenSession(userId, sessionTenantId, null))));
        _authService.ValidateRefreshToken(userId, command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(user));
        _tokenService.IssueAccessToken(
                user,
                Arg.Is<AccessTokenIssueOptions>(options =>
                    options.TenantId == sessionTenantId && options.ActorUserId == null))
            .Returns(newAccessToken);
        _tokenService.IssueRefreshToken().Returns(newRefreshToken);
        _authService.StoreRefreshToken(user.Id, newRefreshToken.Token, newRefreshToken.ExpireAt, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(newAccessToken);
        _tokenService.DidNotReceive().IssueAccessToken(user);
    }

    [Fact]
    public async Task Handle_AssumedSession_RemintsWithActorAndTargetTenant()
    {
        var command = new RefreshTokenCommand("access_token", "refresh_token");
        var userId = 7L;
        const long assumedTenantId = 99;
        var user = new User(userId, SampleData.TENANT_ID, "admin", "admin@example.com", true);
        var newAccessToken = new TokenDto("assumed_access_token", DateTime.UtcNow.AddMinutes(15));
        var newRefreshToken = new TokenDto("new_refresh_token", DateTime.UtcNow.AddDays(7));

        _tokenService.ReadAccessTokenSessionAsync(command.AccessToken, false)
            .Returns(Task.FromResult(Result.Success(new AccessTokenSession(userId, assumedTenantId, userId))));
        _authService.ValidateRefreshToken(userId, command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(user));
        _tokenService.IssueAccessToken(user, Arg.Is<AccessTokenIssueOptions>(
                options => options.TenantId == assumedTenantId
                    && options.ActorUserId == userId
                    && options.AccessExpiryMinutes == AssumeTenantSession.AccessExpiryMinutes))
            .Returns(newAccessToken);
        _tokenService.IssueRefreshToken().Returns(newRefreshToken);
        _authService.StoreRefreshToken(user.Id, newRefreshToken.Token, newRefreshToken.ExpireAt, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(newAccessToken);
        _tokenService.DidNotReceive().IssueAccessToken(user);
    }

    [Fact]
    public async Task Handle_RefreshTokenNotStored_ReturnsError()
    {
        var command = new RefreshTokenCommand("access_token", "refresh_token");
        var userId = 1L;
        var user = new User(userId, SampleData.TENANT_ID, "testuser", "test@example.com", true);
        var newAccessToken = new TokenDto("new_access_token", DateTime.UtcNow.AddMinutes(15));
        var newRefreshToken = new TokenDto("new_refresh_token", DateTime.UtcNow.AddDays(7));

        _tokenService.ReadAccessTokenSessionAsync(command.AccessToken, false)
            .Returns(Task.FromResult(Result.Success(new AccessTokenSession(userId, SampleData.TENANT_ID, null))));
        _authService.ValidateRefreshToken(userId, command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(user));
        _tokenService.IssueAccessToken(
                user,
                Arg.Is<AccessTokenIssueOptions>(options =>
                    options.TenantId == SampleData.TENANT_ID && options.ActorUserId == null))
            .Returns(newAccessToken);
        _tokenService.IssueRefreshToken().Returns(newRefreshToken);
        _authService.StoreRefreshToken(user.Id, newRefreshToken.Token, newRefreshToken.ExpireAt, Arg.Any<CancellationToken>())
            .Returns(Result.Error());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError().Should().BeTrue();
    }
}

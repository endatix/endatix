using MediatR;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.Login;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Core.Tests.UseCases.Identity.Login;

public class LoginHandlerTests
{
    private readonly IAuthService _authService;
    private readonly IUserTokenService _tokenService;
    private readonly ICurrentUserAuthorizationService _permissionService;
    private readonly IMediator _mediator;
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _authService = Substitute.For<IAuthService>();
        _tokenService = Substitute.For<IUserTokenService>();
        _mediator = Substitute.For<IMediator>();
        _permissionService = Substitute.For<ICurrentUserAuthorizationService>();
        _handler = new LoginHandler(_authService, _tokenService, _permissionService, _mediator);
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
        _tokenService.IssueAccessToken(user).Returns(accessToken);
        _tokenService.IssueRefreshToken().Returns(refreshToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().Be(accessToken);
        result.Value.RefreshToken.Should().Be(refreshToken);

        await _authService.Received(1).StoreRefreshToken(user.Id, refreshToken.Token, refreshToken.ExpireAt, Arg.Any<CancellationToken>());
    }
}

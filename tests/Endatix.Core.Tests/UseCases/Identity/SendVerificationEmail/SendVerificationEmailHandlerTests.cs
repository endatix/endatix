using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.SendVerificationEmail;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Endatix.Core.Tests.UseCases.Identity.SendVerificationEmail;

public class SendVerificationEmailHandlerTests
{
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly IUserService _userService;
    private readonly ILogger<SendVerificationEmailHandler> _logger;
    private readonly SendVerificationEmailHandler _sut;

    public SendVerificationEmailHandlerTests()
    {
        _emailVerificationService = Substitute.For<IEmailVerificationService>();
        _userService = Substitute.For<IUserService>();
        _logger = Substitute.For<ILogger<SendVerificationEmailHandler>>();
        _sut = new SendVerificationEmailHandler(_emailVerificationService, _userService, _logger);
    }

    [Fact]
    public async Task Handle_ValidEmail_CreatesTokenSuccessfully()
    {
        // Arrange
        var email = "test@example.com";
        var userId = 123L;
        var user = new User(userId, "testuser", email, false);
        var token = new EmailVerificationToken(userId, "new-token", DateTime.UtcNow.AddHours(24));
        var command = new SendVerificationEmailCommand(email);

        _userService.GetUserAsync(email, Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));
        _emailVerificationService.CreateVerificationTokenAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Success(token));

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        await _userService.Received(1).GetUserAsync(email, Arg.Any<CancellationToken>());
        await _emailVerificationService.Received(1).CreateVerificationTokenAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsSuccessToPreventEnumeration()
    {
        // Arrange
        var email = "nonexistent@example.com";
        var command = new SendVerificationEmailCommand(email);

        _userService.GetUserAsync(email, Arg.Any<CancellationToken>())
            .Returns(Result.NotFound());

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        await _userService.Received(1).GetUserAsync(email, Arg.Any<CancellationToken>());
        await _emailVerificationService.DidNotReceive().CreateVerificationTokenAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserAlreadyVerified_ReturnsSuccessToPreventEnumeration()
    {
        // Arrange
        var email = "verified@example.com";
        var userId = 123L;
        var user = new User(userId, "testuser", email, true);
        var command = new SendVerificationEmailCommand(email);

        _userService.GetUserAsync(email, Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        await _userService.Received(1).GetUserAsync(email, Arg.Any<CancellationToken>());
        await _emailVerificationService.DidNotReceive().CreateVerificationTokenAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TokenCreationFails_ReturnsSuccessButLogsError()
    {
        // Arrange
        var email = "test@example.com";
        var userId = 123L;
        var user = new User(userId, "testuser", email, false);
        var command = new SendVerificationEmailCommand(email);

        _userService.GetUserAsync(email, Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));
        _emailVerificationService.CreateVerificationTokenAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Error("Token creation failed"));

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        await _userService.Received(1).GetUserAsync(email, Arg.Any<CancellationToken>());
        await _emailVerificationService.Received(1).CreateVerificationTokenAsync(userId, Arg.Any<CancellationToken>());
    }
} 
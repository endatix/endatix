using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.ActivateInvite;

namespace Endatix.Core.Tests.UseCases.Identity.ActivateInvite;

public sealed class ActivateInviteHandlerTests
{
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ActivateInviteHandler _handler;

    public ActivateInviteHandlerTests()
    {
        _emailVerificationService = Substitute.For<IEmailVerificationService>();
        _handler = new ActivateInviteHandler(_emailVerificationService);
    }

    [Fact]
    public async Task Handle_WithValidInvite_ReturnsActivatedUser()
    {
        // Arrange
        var command = new ActivateInviteCommand("valid-token", "NewPassword123!");
        var user = new User(123L, "user@example.com", "user@example.com", isVerified: true);

        _emailVerificationService.ActivateInviteAsync(
                command.Token,
                command.Password,
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(user);
        await _emailVerificationService.Received(1).ActivateInviteAsync(
            command.Token,
            command.Password,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidInvite_ReturnsServiceError()
    {
        // Arrange
        var command = new ActivateInviteCommand("invalid-token", "NewPassword123!");
        var invalidResult = Result<User>.Invalid(new ValidationError("Invite token has expired"));

        _emailVerificationService.ActivateInviteAsync(
                command.Token,
                command.Password,
                Arg.Any<CancellationToken>())
            .Returns(invalidResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainSingle(error => error.ErrorMessage == "Invite token has expired");
    }
}

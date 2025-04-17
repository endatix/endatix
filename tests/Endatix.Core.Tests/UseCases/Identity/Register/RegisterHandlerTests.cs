using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.Register;
using MediatR;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Identity.Register;

public class RegisterHandlerTests
{
    private readonly IUserRegistrationService _userRegistrationService;
    private readonly IMediator _mediator;
    private readonly RegisterHandler _handler;

    public RegisterHandlerTests()
    {
        _userRegistrationService = Substitute.For<IUserRegistrationService>();
        _mediator = Substitute.For<IMediator>();
        _handler = new RegisterHandler(_userRegistrationService, _mediator);
    }

    [Fact]
    public async Task Handle_RegistrationFails_ReturnsFailureResult()
    {
        // Arrange
        var email = "test@example.com";
        var password = "password";
        var request = new RegisterCommand(email, password);
        var failureResult = Result<User>.Error("Registration failed");
        
        _userRegistrationService.RegisterUserAsync(email, password, Arg.Any<CancellationToken>())
            .Returns(failureResult);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Registration failed");
    }

    [Fact]
    public async Task Handle_SuccessfulRegistration_PublishesEventAndReturnsSuccess()
    {
        // Arrange
        var email = "test@example.com";
        var password = "password";
        var tenantId = 1L;
        var request = new RegisterCommand(email, password);
        var user = new User(1, tenantId, email, email, true);
        var successResult = Result<User>.Success(user);

        _userRegistrationService.RegisterUserAsync(email, password, Arg.Any<CancellationToken>())
            .Returns(successResult);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(user);

        await _mediator.Received(1).Publish(
            Arg.Is<UserRegisteredEvent>(e => e.User == user),
            Arg.Any<CancellationToken>()
        );
    }
}

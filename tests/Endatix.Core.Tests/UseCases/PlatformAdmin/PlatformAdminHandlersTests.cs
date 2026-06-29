using Endatix.Core.Abstractions;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.PlatformAdmin.GrantPlatformAdmin;
using Endatix.Core.UseCases.PlatformAdmin.RevokePlatformAdmin;
using MediatR;

namespace Endatix.Core.Tests.UseCases.PlatformAdmin;

public sealed class PlatformAdminHandlersTests
{
    private readonly IRoleManagementService _roleManagementService;
    private readonly IMediator _mediator;

    public PlatformAdminHandlersTests()
    {
        _roleManagementService = Substitute.For<IRoleManagementService>();
        _mediator = Substitute.For<IMediator>();
    }

    [Fact]
    public async Task GrantPlatformAdminHandler_WhenGrantSucceeds_PublishesGrantedEvent()
    {
        // Arrange
        const long userId = 123;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        GrantPlatformAdminHandler handler = new(_roleManagementService, _mediator);
        _roleManagementService
            .GrantPlatformAdminAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        Result<string> result = await handler.Handle(new GrantPlatformAdminCommand(userId), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _mediator.Received(1).Publish(
            Arg.Is<PlatformAdminGrantedEvent>(@event => @event.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GrantPlatformAdminHandler_WhenGrantFails_DoesNotPublishGrantedEvent()
    {
        // Arrange
        const long userId = 123;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        GrantPlatformAdminHandler handler = new(_roleManagementService, _mediator);
        _roleManagementService
            .GrantPlatformAdminAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Error("Grant failed."));

        // Act
        Result<string> result = await handler.Handle(new GrantPlatformAdminCommand(userId), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await _mediator
            .DidNotReceive()
            .Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokePlatformAdminHandler_WhenRevokeSucceeds_PublishesRevokedEvent()
    {
        // Arrange
        const long userId = 123;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RevokePlatformAdminHandler handler = new(_roleManagementService, _mediator);
        _roleManagementService
            .RevokePlatformAdminAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        Result<string> result = await handler.Handle(new RevokePlatformAdminCommand(userId), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _mediator.Received(1).Publish(
            Arg.Is<PlatformAdminRevokedEvent>(@event => @event.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokePlatformAdminHandler_WhenRevokeFails_DoesNotPublishRevokedEvent()
    {
        // Arrange
        const long userId = 123;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RevokePlatformAdminHandler handler = new(_roleManagementService, _mediator);
        _roleManagementService
            .RevokePlatformAdminAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Error("Revoke failed."));

        // Act
        Result<string> result = await handler.Handle(new RevokePlatformAdminCommand(userId), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await _mediator
            .DidNotReceive()
            .Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }
}

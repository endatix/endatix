using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Endatix.Core.Infrastructure.Messaging;
using System.Diagnostics;

namespace Endatix.Core.Tests.Infrastructure.Messaging;

public class TaskToThreadPoolPublisherTests
{
    private const int ARBITRARY_LOW_DURATION_IN_MS = 50;
    private readonly IServiceProvider _serviceProvider;
    public TaskToThreadPoolPublisherTests()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<TestNotification1>();
            cfg.NotificationPublisher = new TaskToThreadPoolPublisher();
            cfg.NotificationPublisherType = typeof(TaskToThreadPoolPublisher);
        });

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task WhenTriggered_PublishNotifications_ShouldBeFireAndForget()
    {
        // Arrange
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var timer = new Stopwatch();

        timer.Start();

        // Act
        await mediator.Publish(new TestNotification1());
        timer.Stop();

        // Assert
        var timeElapsed = timer.ElapsedMilliseconds;
        timeElapsed.Should().BeLessThan(ARBITRARY_LOW_DURATION_IN_MS, because: $"{ARBITRARY_LOW_DURATION_IN_MS} is less than the duration of the NotificationHandlers, which are 200ms each. Passing shows that Publish is fire and forget");
    }

    [Fact]
    public async Task WhenNotificationHandlerThrows_ControlFlowShouldNotThrow()
    {
        // Arrange
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Act
        var action = async () => await mediator.Publish(new TestNotification2());

        // Assert
        await action.Should().NotThrowAsync<Exception>();
    }

    public class TestNotification1 : INotification
    {
        public class SleepyHandler : INotificationHandler<TestNotification1>
        {
            public async Task Handle(TestNotification1 notification, CancellationToken cancellationToken)
                => await Task.Delay(250, cancellationToken);
        }
        public class AnotherSleepyHandler : INotificationHandler<TestNotification1>
        {
            public async Task Handle(TestNotification1 notification, CancellationToken cancellationToken)
                => await Task.Delay(300, cancellationToken);
        }
    }



    public class TestNotification2 : INotification
    {
        public class AnotherSleepyHandler : INotificationHandler<TestNotification1>
        {
            public Task Handle(TestNotification1 notification, CancellationToken cancellationToken)
                => throw new Exception("Fatal exception during task execution");
        }
    }
}
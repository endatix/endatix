using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;
using static Endatix.Infrastructure.Tests.Features.Outbox.OutboxWorkTests;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

/// <summary>What one AddOutboxWork call refuses at registration, and what the startup check refuses after it.</summary>
public sealed class OutboxWorkRegistrationTests
{
    [Fact]
    public void AddOutboxWork_SameWorkTwice_Throws()
    {
        // Arrange — a second registration would run the work twice inline and subscribe its job twice.
        var services = new ServiceCollection();
        services.AddOutboxWork<WorkStub>(Defaults);

        // Act
        var registerAgain = () => services.AddOutboxWork<WorkStub>(Defaults);

        // Assert
        registerAgain.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain(typeof(WorkStub).FullName!);
    }

    [Fact]
    public void AddOutboxWork_AfterAPlainInlineRegistration_Throws()
    {
        // Arrange — the docs' plain AddScoped registration kept next to AddOutboxWork.
        var services = new ServiceCollection();
        services.AddScoped<IOutboxIntegrationEventHandler, WorkStub>();

        // Act
        var register = () => services.AddOutboxWork<WorkStub>(Defaults);

        // Assert
        register.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain(typeof(WorkStub).FullName!);
    }

    [Fact]
    public void Validate_HandlerRegisteredTwice_Throws()
    {
        // Arrange — a plain registration added after AddOutboxWork, which registration cannot see.
        var services = new ServiceCollection();
        services.AddOutboxWork<WorkStub>(Defaults);
        var subscriptions = services.BuildServiceProvider().GetRequiredService<OutboxSubscriptions>();

        // Act
        var validate = () => subscriptions.Validate([new WorkStub(), new WorkStub()]);

        // Assert
        validate.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("registered 2 times");
    }

    [Fact]
    public void Validate_WorkWhoseEventTypesMissAJobEvent_Throws()
    {
        // Arrange — the job subscribes HandledEventTypes, but the inline handler lists fewer events.
        var services = new ServiceCollection();
        services.AddOutboxWork<DriftingWorkStub>(Defaults);
        var subscriptions = services.BuildServiceProvider().GetRequiredService<OutboxSubscriptions>();

        // Act
        var validate = () => subscriptions.Validate([new DriftingWorkStub()]);

        // Assert
        validate.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("z.happened");
    }

    [Fact]
    public void Validate_PlainHandlerBesideAWorkOnItsEvent_ThrowsThoughTheWorksJobSubscribesIt()
    {
        // Arrange — a plain inline handler of this assembly on the work's event; only the work has a job.
        var services = new ServiceCollection();
        services.AddOutboxWork<WorkStub>(Defaults);
        var subscriptions = services.BuildServiceProvider().GetRequiredService<OutboxSubscriptions>();
        var plain = new InlineHandlerStub(["x.happened"]);

        // Act
        var validate = () => subscriptions.Validate([new WorkStub(), plain]);

        // Assert
        validate.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain(typeof(InlineHandlerStub).FullName!);
    }

    [Fact]
    public void Validate_WorkRegisteredInlineOnly_ThrowsThoughItsAssemblySubscribesTheEvent()
    {
        // Arrange — another job of this assembly subscribes x.happened, but none is this work's.
        var services = new ServiceCollection();
        services.AddOutboxJobSubscription(
            "x.happened", message => new JobQueueIntegrationEventPublisherTests.FirstPayload(message.Id));
        var subscriptions = services.BuildServiceProvider().GetRequiredService<OutboxSubscriptions>();

        // Act
        var validate = () => subscriptions.Validate([new WorkStub()]);

        // Assert
        validate.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain(typeof(WorkStub).FullName!);
    }

    /// <summary>A work whose inline EventTypes list fewer events than its job subscribes.</summary>
    internal sealed class DriftingWorkStub : IOutboxWork<DriftingWorkStub>
    {
        public static string JobType => "TestDriftingWork";

        public static IReadOnlyCollection<string> HandledEventTypes { get; } = ["x.happened", "z.happened"];

        public IReadOnlyCollection<string> EventTypes { get; } = ["x.happened"];

        public OutboxWork Read(IOutboxMessage message) => OutboxWork.None;
    }
}

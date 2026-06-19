using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Features.Outbox;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

public class OutboxIntegrationEventDispatcherTests
{
    private const long AmbientTenantId = 999;
    private readonly OutboxIntegrationEventDispatcher _sut = new();

    [Fact]
    public void Capture_WithIntegrationEvent_BuildsOutboxMessageFromTheEvent()
    {
        // Arrange
        var aggregate = new TestAggregate(tenantId: 42);
        aggregate.RaiseIntegrationEvent(formId: 123, name: "Survey");

        // Act
        var messages = _sut.Capture([aggregate], AmbientTenantId);

        // Assert
        messages.Should().ContainSingle();
        var message = messages[0];
        message.EventType.Should().Be("test.created");
        message.SchemaVersion.Should().Be(2);
        message.TenantId.Should().Be(42, "a tenant-owned aggregate supplies the authoritative tenant");
        message.Status.Should().Be(OutboxMessageStatus.Pending);
        message.Attempts.Should().Be(0);
        message.Payload.Should().Contain("\"formId\":123").And.Contain("\"name\":\"Survey\"");
    }

    [Fact]
    public void Capture_WhenSourceIsNotTenantOwned_UsesAmbientTenantId()
    {
        // Arrange
        var aggregate = new NonTenantAggregate();
        aggregate.RaiseIntegrationEvent(formId: 1, name: "x");

        // Act
        var messages = _sut.Capture([aggregate], AmbientTenantId);

        // Assert
        messages.Should().ContainSingle();
        messages[0].TenantId.Should().Be(AmbientTenantId);
    }

    [Fact]
    public void Capture_IgnoresPlainDomainEventsThatAreNotIntegrationEvents()
    {
        // Arrange
        var aggregate = new TestAggregate(tenantId: 42);
        aggregate.RaisePlainDomainEvent();

        // Act
        var messages = _sut.Capture([aggregate], AmbientTenantId);

        // Assert
        messages.Should().BeEmpty();
    }

    [Fact]
    public void Capture_ClearsTheEntityDomainEventsAfterCapturing()
    {
        // Arrange
        var aggregate = new TestAggregate(tenantId: 42);
        aggregate.RaiseIntegrationEvent(formId: 1, name: "x");

        // Act
        _sut.Capture([aggregate], AmbientTenantId);

        // Assert
        aggregate.DomainEvents.Should().BeEmpty("captured events must not be re-processed");
    }

    [Fact]
    public void Capture_WithMultipleEventsAcrossEntities_BuildsOneMessagePerIntegrationEvent()
    {
        // Arrange
        var first = new TestAggregate(tenantId: 1);
        first.RaiseIntegrationEvent(formId: 1, name: "a");
        first.RaiseIntegrationEvent(formId: 2, name: "b");
        var second = new TestAggregate(tenantId: 2);
        second.RaiseIntegrationEvent(formId: 3, name: "c");

        // Act
        var messages = _sut.Capture([first, second], AmbientTenantId);

        // Assert
        messages.Should().HaveCount(3);
        messages.Select(m => m.TenantId).Should().Equal(1, 1, 2);
    }

    private sealed record TestPayload(long FormId, string Name);

    private sealed class TestIntegrationEvent(long formId, string name) : DomainEventBase, IIntegrationEvent
    {
        private readonly TestPayload _payload = new(formId, name);
        public string EventType => "test.created";
        public int SchemaVersion => 2;
        public object GetPayload() => _payload;
    }

    private sealed class PlainDomainEvent : DomainEventBase;

    private sealed class TestAggregate(long tenantId) : TenantEntity(tenantId)
    {
        public void RaiseIntegrationEvent(long formId, string name)
            => RegisterDomainEvent(new TestIntegrationEvent(formId, name));

        public void RaisePlainDomainEvent() => RegisterDomainEvent(new PlainDomainEvent());
    }

    private sealed class NonTenantAggregate : BaseEntity
    {
        public void RaiseIntegrationEvent(long formId, string name)
            => RegisterDomainEvent(new TestIntegrationEvent(formId, name));
    }
}

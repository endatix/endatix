using Endatix.Core.Entities;
using Endatix.Core.Events;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

/// <summary>
/// Phase 3b: the Form aggregate raises the integration events (captured to the outbox → webhooks) from its
/// domain methods, and bumps Revision on each business mutation.
/// </summary>
public class FormIntegrationEventTests
{
    private static Form CreateForm(bool isEnabled = false) =>
        new(tenantId: 1, name: "Test Form", isEnabled: isEnabled) { Id = 100 };

    [Fact]
    public void RaiseCreated_registers_FormCreatedEvent_and_keeps_revision_at_1()
    {
        var form = CreateForm();

        form.RaiseCreated();

        form.DomainEvents.OfType<FormCreatedEvent>().Should().ContainSingle();
        form.Revision.Should().Be(1, "creation is revision 1");
    }

    [Fact]
    public void RaiseUpdated_registers_FormUpdatedEvent_and_bumps_revision()
    {
        var form = CreateForm();

        form.RaiseUpdated();

        form.DomainEvents.OfType<FormUpdatedEvent>().Should().ContainSingle();
        form.Revision.Should().Be(2);
    }

    [Fact]
    public void SetEnabled_when_changed_flips_state_raises_event_and_bumps_revision()
    {
        var form = CreateForm(isEnabled: false);

        form.SetEnabled(true);

        form.IsEnabled.Should().BeTrue();
        form.Revision.Should().Be(2);
        form.DomainEvents.OfType<FormEnabledStateChangedEvent>().Should().ContainSingle()
            .Which.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void SetEnabled_when_unchanged_is_a_noop()
    {
        var form = CreateForm(isEnabled: true);

        form.SetEnabled(true);

        form.Revision.Should().Be(1);
        form.DomainEvents.OfType<FormEnabledStateChangedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Delete_registers_FormDeletedEvent_and_bumps_revision()
    {
        var form = CreateForm();

        form.Delete();

        form.IsDeleted.Should().BeTrue();
        form.DomainEvents.OfType<FormDeletedEvent>().Should().ContainSingle();
        form.Revision.Should().Be(2);
    }
}

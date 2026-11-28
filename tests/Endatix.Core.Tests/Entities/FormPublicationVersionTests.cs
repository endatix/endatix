using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Versioning;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

public sealed class FormPublicationVersionTests
{
    [Fact]
    public void Publish_ActivatesAnImmutableVersionAndRaisesOneEvent()
    {
        var form = Form.Create(42, new FormCreateArgs(1, "Survey"));
        var stamp = new ContentVersionStamp(DateTime.UtcNow, 7);
        var definition = FormDefinition.FromDraft(form, new(2, 11, "{}"), stamp);
        form.Publish(definition, "Ready");

        form.ActiveDefinition.Should().BeSameAs(definition);
        form.Revision.Should().Be(2);
        var raised = form.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<FormDefinitionUpdatedEvent>().Subject;
        JsonSerializer.SerializeToElement(raised.GetPayload()).GetProperty("note").GetString().Should().Be("Ready");
        definition.PublishedFromDraftVersion.Should().Be(11);
        definition.Number.Should().Be(2);
        definition.CreatedBy.Should().Be(7);
        definition.CreatedAt.Should().Be(stamp.At);
        definition.ContentHash.Should().Be("44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a");
        definition.Invoking(row => row.UpdateSchema("[]")).Should().Throw<InvalidOperationException>();
        definition.Invoking(row => row.UpdateDraftStatus(true)).Should().Throw<InvalidOperationException>();
        definition.Invoking(row => row.Delete()).Should().Throw<InvalidOperationException>();
    }
}

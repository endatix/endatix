using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Reporting.Tests.Features.Outbox;

public sealed class CompileFormSchemaOutboxHandlerTests
{
    private const long TenantId = 42;
    private const long FormId = 100;
    private const long FormDefinitionId = 200;

    private readonly IFormSchemaProcessor _schemaProcessor = Substitute.For<IFormSchemaProcessor>();

    private CompileFormSchemaOutboxHandler CreateSut() =>
        new(_schemaProcessor);

    [Fact]
    public void EventTypes_IncludesFormDefinitionUpdated()
    {
        CreateSut().EventTypes.Should().Contain(FormDefinitionUpdatedEvent.EventTypeName);
    }

    [Fact]
    public async Task HandleAsync_WithValidPayload_CallsProcessorWithIds()
    {
        Form form = new(TenantId, "Survey", isEnabled: true) { Id = FormId };
        FormDefinition definition = new(TenantId, jsonData: "{}") { Id = FormDefinitionId };
        form.AddFormDefinition(definition);
        string payload = ReportingOutboxTestHelpers.SerializePayload(new FormDefinitionUpdatedEvent(form, definition).GetPayload());
        ReportingOutboxTestHelpers.FakeOutboxMessage message = new(
            Id: 1,
            EventType: FormDefinitionUpdatedEvent.EventTypeName,
            Payload: payload,
            TenantId: TenantId);

        await CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await _schemaProcessor.Received(1).ProcessAsync(
            TenantId,
            FormId,
            FormDefinitionId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithMissingFormId_ThrowsAndDoesNotCallProcessor()
    {
        ReportingOutboxTestHelpers.FakeOutboxMessage message = new(
            Id: 9,
            EventType: FormDefinitionUpdatedEvent.EventTypeName,
            Payload: """{"tenantId":"42","formDefinitionId":"200"}""",
            TenantId: TenantId);

        Func<Task> act = () => CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing a valid formId*");
        await _schemaProcessor.DidNotReceive().ProcessAsync(
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithTenantMismatch_ThrowsAndDoesNotCallProcessor()
    {
        ReportingOutboxTestHelpers.FakeOutboxMessage message = new(
            Id: 5,
            EventType: FormDefinitionUpdatedEvent.EventTypeName,
            Payload: """{"tenantId":"99","formId":"100","formDefinitionId":"200"}""",
            TenantId: TenantId);

        Func<Task> act = () => CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenantId mismatch*");
        await _schemaProcessor.DidNotReceive().ProcessAsync(
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenProcessorThrows_PropagatesException()
    {
        Form form = new(TenantId, "Survey", isEnabled: true) { Id = FormId };
        FormDefinition definition = new(TenantId, jsonData: "{}") { Id = FormDefinitionId };
        form.AddFormDefinition(definition);
        string payload = ReportingOutboxTestHelpers.SerializePayload(new FormDefinitionUpdatedEvent(form, definition).GetPayload());
        ReportingOutboxTestHelpers.FakeOutboxMessage message = new(
            Id: 2,
            EventType: FormDefinitionUpdatedEvent.EventTypeName,
            Payload: payload,
            TenantId: TenantId);
        _schemaProcessor
            .ProcessAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("compile failed")));

        Func<Task> act = () => CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("compile failed");
    }
}

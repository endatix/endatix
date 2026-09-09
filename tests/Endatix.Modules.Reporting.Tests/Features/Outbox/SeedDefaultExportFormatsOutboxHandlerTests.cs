using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Modules.Reporting.Features.ExportFormats;
using Endatix.Modules.Reporting.Features.Outbox;

namespace Endatix.Modules.Reporting.Tests.Features.Outbox;

public sealed class SeedDefaultExportFormatsOutboxHandlerTests
{
    private const long TenantId = 9101;

    private readonly IDefaultExportFormatsSeeder _seeder = Substitute.For<IDefaultExportFormatsSeeder>();

    private SeedDefaultExportFormatsOutboxHandler CreateSut() => new(_seeder);

    [Fact]
    public void EventTypes_IncludesTenantCreated()
    {
        CreateSut().EventTypes.Should().Contain(TenantCreatedEvent.EventTypeName);
    }

    [Fact]
    public async Task HandleAsync_WithValidPayload_SeedsThatTenant()
    {
        Tenant tenant = new("Acme", "abcd1234") { Id = TenantId };
        string payload = ReportingOutboxTestHelpers.SerializePayload(new TenantCreatedEvent(tenant).GetPayload());
        ReportingOutboxTestHelpers.FakeOutboxMessage message = new(
            Id: 1,
            EventType: TenantCreatedEvent.EventTypeName,
            Payload: payload,
            TenantId: AuthConstants.DEFAULT_TENANT_ID);

        await CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await _seeder.Received(1).SeedAsync(TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithMissingTenantId_ThrowsAndDoesNotSeed()
    {
        ReportingOutboxTestHelpers.FakeOutboxMessage message = new(
            Id: 2,
            EventType: TenantCreatedEvent.EventTypeName,
            Payload: """{"name":"Acme"}""",
            TenantId: AuthConstants.DEFAULT_TENANT_ID);

        Func<Task> act = () => CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing a valid tenantId*");
        await _seeder.DidNotReceive().SeedAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }
}

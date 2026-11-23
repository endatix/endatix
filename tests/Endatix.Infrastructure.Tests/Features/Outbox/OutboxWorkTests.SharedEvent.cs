using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

/// <summary>
/// App-level work and tenant work subscribed to the same event, each filtering out the other's rows with
/// <c>onlyWhen</c>: a row one work skips never reaches that work's scope check, so it cannot block the other.
/// </summary>
public sealed partial class OutboxWorkTests
{
    [Fact]
    public async Task PublishAsync_AppLevelWorkSkipsTenantRow_EnqueuesTheOtherSubscribersJob()
    {
        // Arrange
        using var provider = BuildSharedEvent();

        // Act
        await PublishAsync(provider, TenantRow());

        // Assert
        var request = _enqueued.Should().ContainSingle().Subject;
        (request.JobType, request.Scope).Should().Be((TenantWorkStub.JobType, JobScope.ForTenant(TenantA)));
    }

    [Fact]
    public async Task PublishAsync_TenantWorkSkipsAppLevelRow_EnqueuesTheSystemJob()
    {
        // Arrange
        using var provider = BuildSharedEvent();

        // Act
        await PublishAsync(provider, new OutboxMessageStub(MessageId, "x.happened", """{"work":"run"}""", NoTenant));

        // Assert
        var request = _enqueued.Should().ContainSingle().Subject;
        (request.JobType, request.Scope).Should().Be((WorkStub.JobType, JobScope.System));
    }

    [Fact]
    public async Task PublishInlineAsync_AppLevelWorkSkipsTenantRow_RunsOnlyTheOtherSubscriber()
    {
        // Arrange
        using var provider = BuildSharedEvent();
        using var scope = provider.CreateScope();
        var appLevelWork = scope.ServiceProvider.GetRequiredService<WorkStub>();
        var tenantWork = scope.ServiceProvider.GetRequiredService<TenantWorkStub>();
        var publisher = scope.ServiceProvider.GetRequiredService<CompositeIntegrationEventPublisher>();

        // Act
        await publisher.PublishAsync(TenantRow(), TestContext.Current.CancellationToken);

        // Assert
        appLevelWork.Ran.Should().BeFalse();
        tenantWork.Ran.Should().BeTrue();
    }

    private ServiceProvider BuildSharedEvent() =>
        BuildWith(
            services => services.AddAppLevelOutboxWork<WorkStub>(Defaults, onlyWhen: message => message.TenantId == NoTenant),
            services => services.AddOutboxWork<TenantWorkStub>(Defaults, onlyWhen: message => message.TenantId != NoTenant));

    private static OutboxMessageStub TenantRow() =>
        new(MessageId, "x.happened", $$"""{"tenantId":"{{TenantA}}","work":"run"}""", TenantA);

    /// <summary>Tenant work on the event <see cref="WorkStub"/> handles.</summary>
    internal sealed class TenantWorkStub : IOutboxWork<TenantWorkStub>
    {
        public static string JobType => "TestTenantWork";

        public static IReadOnlyCollection<string> HandledEventTypes { get; } = ["x.happened"];

        public bool Ran { get; private set; }

        public OutboxWork Read(IOutboxMessage message) => OutboxWork.For(_ =>
        {
            Ran = true;
            return Task.CompletedTask;
        });
    }
}

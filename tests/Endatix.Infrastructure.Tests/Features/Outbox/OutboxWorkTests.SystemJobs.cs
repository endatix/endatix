using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

/// <summary>Outbox work registered with <c>AddAppLevelOutboxWork</c>, which runs as system jobs.</summary>
public sealed partial class OutboxWorkTests
{
    private const long NoTenant = 0;

    private ServiceProvider BuildAppLevel() =>
        BuildWith(services => services.AddAppLevelOutboxWork<WorkStub>(Defaults));

    [Fact]
    public async Task PublishAsync_AppLevelWorkAndAppLevelRow_EnqueuesASystemJob()
    {
        // Arrange
        using var provider = BuildAppLevel();

        // Act
        await PublishAsync(provider, new OutboxMessageStub(MessageId, "x.happened", """{"work":"run"}""", NoTenant));

        // Assert
        var request = _enqueued.Should().ContainSingle().Subject;
        request.Scope.Should().Be(JobScope.System);
        request.DedupKey.Should().Be($"{MessageId}:{WorkStub.JobType}");
    }

    [Fact]
    public async Task PublishAsync_AppLevelWorkAndTenantRow_ThrowsAndEnqueuesNothing()
    {
        // Arrange — a row that names a tenant is that tenant's work, never a system job's.
        using var provider = BuildAppLevel();

        // Act
        var publish = () => PublishAsync(
            provider, new OutboxMessageStub(MessageId, "x.happened", $$"""{"tenantId":"{{TenantA}}"}""", TenantA));

        // Assert
        await publish.Should().ThrowAsync<InvalidOperationException>();
        _enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishInlineAsync_AppLevelWorkAndTenantRow_ThrowsWithoutRunningTheWork()
    {
        // Arrange — with job delivery off, the inline path refuses the row the job path refuses.
        using var provider = BuildAppLevel();

        // Act
        var (thrown, work) = await PublishInlineAsync(
            provider, new OutboxMessageStub(MessageId, "x.happened", $$"""{"tenantId":"{{TenantA}}","work":"run"}""", TenantA));

        // Assert
        thrown.Should().NotBeNull();
        work.Ran.Should().BeFalse();
    }

    [Fact]
    public async Task PublishInlineAsync_AppLevelWorkAndAppLevelRow_RunsTheWork()
    {
        // Arrange
        using var provider = BuildAppLevel();

        // Act
        var (thrown, work) = await PublishInlineAsync(
            provider, new OutboxMessageStub(MessageId, "x.happened", """{"work":"run"}""", NoTenant));

        // Assert
        thrown.Should().BeNull();
        work.Ran.Should().BeTrue();
    }

    [Fact]
    public async Task PublishInlineAsync_TenantWorkAndTenantRow_RunsTheWork()
    {
        // Arrange — the refusal belongs to app-level work alone.
        using var provider = Build();

        // Act
        var (thrown, work) = await PublishInlineAsync(
            provider, new OutboxMessageStub(MessageId, "x.happened", $$"""{"tenantId":"{{TenantA}}","work":"run"}""", TenantA));

        // Assert
        thrown.Should().BeNull();
        work.Ran.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_AppLevelWorkWithOnlyWhen_EnqueuesASystemJobUnlessTheMessageNeedsNothing()
    {
        // Arrange
        using var provider = BuildWith(services =>
            services.AddAppLevelOutboxWork<WorkStub>(Defaults, onlyWhen: WorkStub.MayNeedWork));

        // Act
        await PublishAsync(provider, new OutboxMessageStub(MessageId, "x.happened", """{"work":"none"}""", NoTenant));
        var skipped = _enqueued;
        await PublishAsync(provider, new OutboxMessageStub(MessageId, "x.happened", """{"work":"run"}""", NoTenant));

        // Assert
        skipped.Should().BeEmpty();
        _enqueued.Should().ContainSingle().Which.Scope.Should().Be(JobScope.System);
    }

    // Publishes inline, as the relay does with job delivery off, and returns what it threw and the work it resolved.
    private static async Task<(Exception? Thrown, WorkStub Work)> PublishInlineAsync(
        IServiceProvider provider,
        IOutboxMessage message)
    {
        using var scope = provider.CreateScope();
        var work = scope.ServiceProvider.GetRequiredService<WorkStub>();
        var publisher = scope.ServiceProvider.GetRequiredService<CompositeIntegrationEventPublisher>();
        try
        {
            await publisher.PublishAsync(message, TestContext.Current.CancellationToken);
            return (null, work);
        }
        catch (InvalidOperationException exception)
        {
            return (exception, work);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SystemJobOfAppLevelWork_RunsTheWorkOnTheAppLevelRow()
    {
        // Arrange
        Seed("""{"work":"run"}""", NoTenant);
        using var provider = BuildAppLevel();

        // Act
        var (result, work) = await RunJobAsync(provider, JobScope.System);

        // Assert
        result.IsSuccess.Should().BeTrue();
        work.Ran.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_SystemJobOfTenantWork_FailsAsWrongScope()
    {
        // Arrange — a system job must not reach an app-level row through work that runs per tenant.
        Seed("""{"work":"run"}""", NoTenant);
        using var provider = Build();

        // Act
        var (result, work) = await RunJobAsync(provider, JobScope.System);

        // Assert
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(OutboxJobFailures.WrongScope);
        work.Ran.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_TenantJobOfAppLevelWork_FailsAsWrongScope()
    {
        // Arrange — a tenant job must not run work that belongs to no tenant.
        Seed("""{"work":"run"}""", NoTenant);
        using var provider = BuildAppLevel();

        // Act
        var (result, work) = await RunJobAsync(provider, TenantA);

        // Assert
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(OutboxJobFailures.WrongScope);
        work.Ran.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_SystemJobAndTenantRow_FailsAsGone()
    {
        // Arrange — the system job reads app-level rows only.
        Seed($$"""{"tenantId":"{{TenantA}}","work":"run"}""", TenantA);
        using var provider = BuildAppLevel();

        // Act
        var (result, work) = await RunJobAsync(provider, JobScope.System);

        // Assert
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(OutboxJobFailures.MessageGone);
        work.Ran.Should().BeFalse();
    }
}

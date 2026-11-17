using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Features.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

/// <summary>Outbox work registered with <c>appLevel: true</c>, which runs as system jobs.</summary>
public sealed partial class OutboxWorkTests
{
    private const long NoTenant = 0;

    private ServiceProvider BuildAppLevel() =>
        BuildWith(services => services.AddOutboxWork<WorkStub>(Defaults, appLevel: true));

    [Fact]
    public void AddOutboxWork_AppLevelAndAppLevelTenant_Throws()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var register = () => services.AddOutboxWork<WorkStub>(
            Defaults, appLevelTenant: WorkStub.ReadNewTenant, appLevel: true);

        // Assert
        register.Should().Throw<ArgumentException>().WithParameterName("appLevel");
    }

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

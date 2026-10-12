using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The audience match-key lock as the web host wires it, against PostgreSQL: each party resolves the lock and the
/// audience context from a scope of its own, as a request would.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class MatchKeyLockIntegrationTests(EndatixIntegrationWebHostFixture fixture)
{
    private const long TenantId = 1;
    private static readonly TimeSpan LongEnoughToTakeAFreeLock = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(10);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BeginExclusiveAsync_WhileAMemberWriterOfTheTenantIsOpen_WaitsUntilItCommits()
    {
        // Arrange
        Assert.SkipWhen(
            fixture.Provider != TestDatabaseProvider.PostgreSql,
            "The audience module is PostgreSQL-only; it is not registered on this provider.");
        await using var writer = fixture.Factory.Services.CreateAsyncScope();
        await using var memberWrite = await BeginSharedAsync(writer);
        await using var keyChange = fixture.Factory.Services.CreateAsyncScope();
        var exclusive = BeginExclusiveAsync(keyChange);
        var tookItWhileTheWriterWasOpen = await Task.WhenAny(exclusive, Task.Delay(LongEnoughToTakeAFreeLock, Cancellation)) == exclusive;

        // Act
        await memberWrite.CommitAsync(Cancellation);

        // Assert
        tookItWhileTheWriterWasOpen.Should().BeFalse();
        await using var keyChangeTransaction = await exclusive.WaitAsync(MaxWait, Cancellation);
        keyChangeTransaction.Should().NotBeNull();
    }

    private static Task<IDbContextTransaction> BeginSharedAsync(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<MatchKeyLock>()
            .BeginSharedAsync(scope.ServiceProvider.GetRequiredService<IAudienceDbContext>(), TenantId, Cancellation);

    private static Task<IDbContextTransaction> BeginExclusiveAsync(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<MatchKeyLock>()
            .BeginExclusiveAsync(scope.ServiceProvider.GetRequiredService<IAudienceDbContext>(), TenantId, Cancellation);
}

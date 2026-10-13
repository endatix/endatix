using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The audience match-key lock as the web host wires it, on whichever provider the run uses: each party resolves the
/// lock and the audience context from a scope of its own, as a request would.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class MatchKeyLockIntegrationTests(EndatixIntegrationWebHostFixture fixture)
{
    private const long TenantId = 101;
    private const long OtherTenantId = 202;
    private static readonly TimeSpan LongEnoughToTakeAFreeLock = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan FreeLockWait = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(5);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BeginSharedAsync_WhileSharedHeld_DoesNotWait()
    {
        // Arrange
        await using var firstWriter = fixture.Factory.Services.CreateAsyncScope();
        await using var firstWrite = await BeginSharedAsync(firstWriter, TenantId);
        await using var secondWriter = fixture.Factory.Services.CreateAsyncScope();

        // Act
        var secondWrite = BeginSharedAsync(secondWriter, TenantId);

        // Assert
        await using var secondTransaction = await secondWrite.WaitAsync(FreeLockWait, Cancellation);
        secondTransaction.Should().NotBeNull();
    }

    [Fact]
    public async Task BeginExclusiveAsync_WhileSharedHeld_WaitsUntilSharedHoldersCommit()
    {
        // Arrange
        await using var firstWriter = fixture.Factory.Services.CreateAsyncScope();
        await using var firstWrite = await BeginSharedAsync(firstWriter, TenantId);
        await using var secondWriter = fixture.Factory.Services.CreateAsyncScope();
        await using var secondWrite = await BeginSharedAsync(secondWriter, TenantId);
        await using var keyChange = fixture.Factory.Services.CreateAsyncScope();
        var exclusive = BeginExclusiveAsync(keyChange, TenantId);
        var tookItWhileBothWereOpen = await TookItWithinAsync(exclusive, LongEnoughToTakeAFreeLock);
        await firstWrite.CommitAsync(Cancellation);
        var tookItWhileOneWasOpen = await TookItWithinAsync(exclusive, LongEnoughToTakeAFreeLock);

        // Act
        await secondWrite.CommitAsync(Cancellation);

        // Assert
        tookItWhileBothWereOpen.Should().BeFalse();
        tookItWhileOneWasOpen.Should().BeFalse();
        await using var keyChangeTransaction = await exclusive.WaitAsync(MaxWait, Cancellation);
        keyChangeTransaction.Should().NotBeNull();
    }

    [Fact]
    public async Task BeginExclusiveAsync_OtherTenantHoldsShared_DoesNotWait()
    {
        // Arrange
        await using var writer = fixture.Factory.Services.CreateAsyncScope();
        await using var memberWrite = await BeginSharedAsync(writer, TenantId);
        await using var keyChange = fixture.Factory.Services.CreateAsyncScope();

        // Act
        var exclusive = BeginExclusiveAsync(keyChange, OtherTenantId);

        // Assert
        await using var keyChangeTransaction = await exclusive.WaitAsync(FreeLockWait, Cancellation);
        keyChangeTransaction.Should().NotBeNull();
    }

    [Fact]
    public async Task BeginExclusiveAsync_CancelledWhileWaiting_LeavesNoTransactionOpen()
    {
        // Arrange
        await using var writer = fixture.Factory.Services.CreateAsyncScope();
        await using var memberWrite = await BeginSharedAsync(writer, TenantId);
        await using var keyChange = fixture.Factory.Services.CreateAsyncScope();
        var db = keyChange.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        cancellation.CancelAfter(LongEnoughToTakeAFreeLock);

        // Act
        var act = () => keyChange.ServiceProvider.GetRequiredService<MatchKeyLock>()
            .BeginExclusiveAsync(db, TenantId, cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        ((DbContext)db).Database.CurrentTransaction.Should().BeNull();
    }

    private static async Task<bool> TookItWithinAsync(Task<IDbContextTransaction> request, TimeSpan wait) =>
        await Task.WhenAny(request, Task.Delay(wait, Cancellation)) == request;

    private static Task<IDbContextTransaction> BeginSharedAsync(AsyncServiceScope scope, long tenantId) =>
        scope.ServiceProvider.GetRequiredService<MatchKeyLock>()
            .BeginSharedAsync(scope.ServiceProvider.GetRequiredService<IAudienceDbContext>(), tenantId, Cancellation);

    private static Task<IDbContextTransaction> BeginExclusiveAsync(AsyncServiceScope scope, long tenantId) =>
        scope.ServiceProvider.GetRequiredService<MatchKeyLock>()
            .BeginExclusiveAsync(scope.ServiceProvider.GetRequiredService<IAudienceDbContext>(), tenantId, Cancellation);
}

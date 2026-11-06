using System.Diagnostics;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Every authenticated request reads the <see cref="AuthorizationStamp"/> row. On SQL Server without
/// <c>READ_COMMITTED_SNAPSHOT</c> (this test database has it off) that read waits for any transaction holding the row,
/// so a role change writes the stamp as its last statement before commit and an open role change holds nothing.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "SqlServer")]
public sealed class AuthorizationStampSqlServerTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private const string RoleName = "StampedRole";
    private static readonly TimeSpan NotHeld = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Stamp_read_is_not_held_behind_an_open_role_change()
    {
        // Arrange — a role change has saved its rows in a transaction that stays open.
        Assert.SkipWhen(fixture.Database.Provider != TestDatabaseProvider.SqlServer, "Asserts SQL Server read locking.");
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        IntegrationSeedBuilder builder = new(world.Services);
        await builder.SeedRoleAsync(world.Tenants[0].Id, RoleName, ct);
        await builder.SeedRolePermissionsAsync(world.Tenants[0].Id, RoleName, [Actions.Forms.View, Actions.Forms.Create], ct);
        var committed = await ReadStampAsync(world, ct);
        TestContext.Current.SendDiagnosticMessage(
            $"READ_COMMITTED_SNAPSHOT on: {await ReadCommittedSnapshotOnAsync(world, ct)}");

        using var writerScope = world.Services.CreateScope();
        var writer = writerScope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await using var roleChange = await writer.Database.BeginTransactionAsync(ct);
        writer.RolePermissions.Remove(await writer.RolePermissions.FirstAsync(ct));
        await writer.SaveChangesAsync(ct);

        // Act
        Stopwatch elapsed = Stopwatch.StartNew();
        var read = ReadStampAsync(world, ct);
        var finished = await Task.WhenAny(read, Task.Delay(NotHeld, ct));
        elapsed.Stop();
        await roleChange.RollbackAsync(ct);

        // Assert
        finished.Should().BeSameAs(read, $"the stamp read waited {elapsed.ElapsedMilliseconds} ms for an open role change");
        (await read).Should().Be(committed);
    }

    private static async Task<Guid> ReadStampAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAuthorizationStampReader>().ReadAsync(ct);
    }

    private static async Task<bool> ReadCommittedSnapshotOnAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        return await db.Database
            .SqlQueryRaw<bool>("SELECT is_read_committed_snapshot_on AS [Value] FROM sys.databases WHERE name = DB_NAME()")
            .SingleAsync(ct);
    }
}

using FluentAssertions;
using PostgreSqlMigrations = Endatix.Persistence.PostgreSql.Migrations.AppEntities;
using SqlServerMigrations = Endatix.Persistence.SqlServer.Migrations.AppEntities;

namespace Endatix.Infrastructure.Tests.Migrations;

/// <summary>
/// Migrations that read embedded SQL scripts, built as often as a process that migrates many databases builds them.
/// Once the runtime recompiles a hot migration with its callees inlined, a script lookup that works its assembly
/// out from the call stack finds the wrong caller and cannot find the script.
/// </summary>
public sealed class MigrationScriptsTests
{
    // Enough builds for the runtime to recompile the migration as optimized code, with pauses so that the
    // recompilation, which runs in the background, lands while the builds go on.
    private const int Builds = 200;

    [Fact]
    public async Task UpOperations_PostgreSqlMigrationBuiltRepeatedly_ReadsScriptFromItsOwnAssembly()
    {
        // Act
        var build = () => BuildRepeatedlyAsync(() => _ = new PostgreSqlMigrations.SeedUserInviteTemplate().UpOperations);

        // Assert
        await build.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpOperations_SqlServerMigrationBuiltRepeatedly_ReadsScriptFromItsOwnAssembly()
    {
        // Act
        var build = () => BuildRepeatedlyAsync(() => _ = new SqlServerMigrations.SeedUserInviteTemplate().UpOperations);

        // Assert
        await build.Should().NotThrowAsync();
    }

    private static async Task BuildRepeatedlyAsync(Action buildOnce)
    {
        for (var build = 0; build < Builds; build++)
        {
            buildOnce();
            await Task.Delay(TimeSpan.FromMilliseconds(build < 60 ? 20 : 5), TestContext.Current.CancellationToken);
        }
    }
}

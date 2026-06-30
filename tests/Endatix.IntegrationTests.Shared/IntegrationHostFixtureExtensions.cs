namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Extension methods on <see cref="IIntegrationTestHostFixture"/> for database reset and world preparation.
/// </summary>
public static class IntegrationHostFixtureExtensions
{
    /// <summary>
    /// Resets the database via the checkpoint and optionally runs the standard seed.
    /// </summary>
    /// <param name="fixture">The test host fixture.</param>
    /// <param name="useStandardSeed">When <c>true</c>, runs the standard seed after reset.</param>
    /// <param name="options">Optional seed options (applies when <paramref name="useStandardSeed"/> is <c>true</c>).</param>
    /// <param name="afterSeed">Optional callback invoked after the seed completes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The seed result if <paramref name="useStandardSeed"/> is <c>true</c>; otherwise <c>null</c>.</returns>
    public static async Task<StandardSeedResult?> ResetDatabaseAsync(
        this IIntegrationTestHostFixture fixture,
        bool useStandardSeed = false,
        StandardSeedOptions? options = null,
        Func<IServiceProvider, StandardSeedResult, CancellationToken, Task>? afterSeed = null,
        CancellationToken cancellationToken = default)
    {
        await fixture.Checkpoint.ResetAsync(
            fixture.Database.ConnectionString,
            fixture.Database.Provider,
            cancellationToken);

        if (!useStandardSeed)
        {
            return null;
        }

        return await fixture.Seed.SeedStandardAsync(options, afterSeed, cancellationToken);
    }

    /// <summary>
    /// Prepares an <see cref="IntegrationTestWorld"/> by optionally resetting the database and running the standard seed.
    /// </summary>
    /// <param name="fixture">The test host fixture.</param>
    /// <param name="options">World options controlling reset, seed, and auth settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<IntegrationTestWorld> PrepareWorldAsync(
        this IIntegrationTestHostFixture fixture,
        IntegrationWorldOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedOptions = options ?? IntegrationWorldOptions.Empty;

        if (resolvedOptions.ResetDatabase)
        {
            await fixture.Checkpoint.ResetAsync(
                fixture.Database.ConnectionString,
                fixture.Database.Provider,
                cancellationToken);
        }

        StandardSeedResult? seedResult = null;
        if (resolvedOptions.SeedOptions is not null)
        {
            var seedOptions = resolvedOptions.DefaultPassword is not null
                ? resolvedOptions.SeedOptions with { DefaultPassword = resolvedOptions.DefaultPassword }
                : resolvedOptions.SeedOptions;

            seedResult = await fixture.Seed.SeedStandardAsync(seedOptions, resolvedOptions.AfterSeed, cancellationToken);
        }

        return new IntegrationTestWorld
        {
            Services = fixture.Services,
            Seed = fixture.Seed,
            SeedResult = seedResult,
            CreateClientFactory = fixture.CreateClient,
            Options = resolvedOptions
        };
    }
}

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Standard seed presets for the integration tests.
/// </summary>
public static class StandardSeedPresets
{
    /// <summary>
    /// The empty standard seed preset.
    /// </summary>
    public static IntegrationWorldOptions Empty { get; } = new();

    /// <summary>
    /// The single tenant standard seed preset.
    /// </summary>
    public static IntegrationWorldOptions SingleTenant { get; } = new()
    {
        SeedOptions = new StandardSeedOptions(
            [
                new StandardSeedTenant(
                    "seed-tenant-a",
                    "seed-admin-a",
                    "seed-admin-a@test.local",
                    "seed-creator-a",
                    "seed-creator-a@test.local",
                    "seed-platform-admin-a",
                    "seed-platform-admin-a@test.local")
            ])
    };

    /// <summary>
    /// The multi tenant standard seed preset.
    /// </summary>
    public static IntegrationWorldOptions MultiTenant { get; } = new()
    {
        SeedOptions = StandardSeedOptions.CreateDefault()
    };
}

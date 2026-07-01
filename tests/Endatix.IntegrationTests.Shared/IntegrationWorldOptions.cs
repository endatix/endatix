namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Options for preparing an <see cref="IntegrationTestWorld"/>.
/// </summary>
public sealed record IntegrationWorldOptions
{
    /// <summary>Whether to reset the database before seeding.</summary>
    public bool ResetDatabase { get; init; } = true;

    /// <summary>Standard seed options, or <c>null</c> to skip seeding.</summary>
    public StandardSeedOptions? SeedOptions { get; init; }

    /// <summary>Optional callback invoked after the standard seed completes.</summary>
    public Func<IServiceProvider, StandardSeedResult, CancellationToken, Task>? AfterSeed { get; init; }

    /// <summary>Default password for seeded users.</summary>
    public string? DefaultPassword { get; init; }

    /// <summary>Auth settings for synthetic JWT creation.</summary>
    public IntegrationTestAuthSettings AuthSettings { get; init; } = IntegrationTestAuthSettings.Default;

    /// <summary>The empty standard seed preset.</summary>
    public static IntegrationWorldOptions Empty => StandardSeedPresets.Empty;

    /// <summary>The single tenant standard seed preset.</summary>
    public static IntegrationWorldOptions SingleTenant => StandardSeedPresets.SingleTenant;

    /// <summary>The multi tenant standard seed preset.</summary>
    public static IntegrationWorldOptions MultiTenant => StandardSeedPresets.MultiTenant;
}

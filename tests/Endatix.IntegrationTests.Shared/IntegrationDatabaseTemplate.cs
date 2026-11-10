namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// What every fixture database starts with. The test process builds one template database per provider on first use,
/// and each <see cref="DatabaseInfrastructureFixture"/> gets a copy of it, so a host that starts on a fixture database
/// finds its migrations already applied.
/// </summary>
/// <remarks>
/// With no template set, a fixture database starts empty and the host migrates it at startup.
/// </remarks>
public static class IntegrationDatabaseTemplate
{
    private static readonly Lock _gate = new();
    private static Func<string, TestDatabaseProvider, CancellationToken, Task>? _migrate;
    private static bool _built;

    /// <summary>
    /// Sets how the template is built: <paramref name="migrate"/> gets the template's connection string and provider, and
    /// applies everything a fixture database should start with, usually by starting a host that migrates every module.
    /// Call it before the first fixture starts, from a <see cref="System.Runtime.CompilerServices.ModuleInitializerAttribute">module initializer</see>
    /// of the test assembly.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A template is already set, or a fixture has already started, so the template is already built.
    /// </exception>
    public static void Use(Func<string, TestDatabaseProvider, CancellationToken, Task> migrate)
    {
        ArgumentNullException.ThrowIfNull(migrate);
        lock (_gate)
        {
            if (_built)
            {
                throw new InvalidOperationException(
                    "The template database is already built, so a later template would not reach any fixture. " +
                    $"Call {nameof(IntegrationDatabaseTemplate)}.{nameof(Use)} from a module initializer.");
            }

            if (_migrate is not null)
            {
                throw new InvalidOperationException(
                    "A template is already set for this test process; a second one would silently replace it.");
            }

            _migrate = migrate;
        }
    }

    /// <summary>The migration to build the template with, or <see langword="null"/> when there is none; no change after this.</summary>
    internal static Func<string, TestDatabaseProvider, CancellationToken, Task>? Seal()
    {
        lock (_gate)
        {
            _built = true;
            return _migrate;
        }
    }
}

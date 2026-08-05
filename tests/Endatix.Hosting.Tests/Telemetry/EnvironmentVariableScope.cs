namespace Endatix.Hosting.Tests.Telemetry;

/// <summary>
/// Sets environment variables for the duration of a test and restores their previous values on
/// dispose — including restoring them to unset when they were unset to begin with.
/// </summary>
/// <remarks>
/// Environment variables are process-global, so tests using this must not run in parallel with each
/// other. See <see cref="TelemetryEnvironmentCollection"/>.
/// </remarks>
internal sealed class EnvironmentVariableScope : IDisposable
{
    private readonly Dictionary<string, string?> _previous = [];

    public EnvironmentVariableScope(params (string Name, string? Value)[] variables)
    {
        foreach (var (name, value) in variables)
        {
            _previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    public void Dispose()
    {
        foreach (var (name, value) in _previous)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}

/// <summary>
/// Serialises every test that mutates OTEL_* environment variables.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class TelemetryEnvironmentCollection
{
    public const string Name = "Telemetry environment";
}

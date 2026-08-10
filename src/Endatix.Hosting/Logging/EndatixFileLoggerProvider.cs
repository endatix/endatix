using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;

namespace Endatix.Hosting.Logging;

/// <summary>
/// The file-logging provider, aliased so its levels are configured under
/// <c>Logging:EndatixFile:LogLevel</c>.
/// </summary>
/// <remarks>
/// <para>
/// This exists only to control the provider alias. Registering
/// <see cref="SerilogLoggerProvider"/> directly would make the level key
/// <c>Logging:Serilog:LogLevel</c>, putting the rotation library's name on the configuration
/// surface — the one thing this feature is meant to keep private. <see cref="SerilogLoggerProvider"/>
/// is sealed, so re-aliasing means delegation rather than inheritance.
/// </para>
/// </remarks>
[ProviderAlias("EndatixFile")]
internal sealed class EndatixFileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly SerilogLoggerProvider _inner;

    public EndatixFileLoggerProvider(Serilog.ILogger logger, bool dispose)
    {
        _inner = new SerilogLoggerProvider(logger, dispose);
    }

    public ILogger CreateLogger(string categoryName) => _inner.CreateLogger(categoryName);

    /// <summary>
    /// Forwards the framework's scope provider to the inner provider.
    /// </summary>
    /// <remarks>
    /// Load-bearing: without it the delegation silently drops every scope property from the file,
    /// and nothing else in the pipeline reports the loss.
    /// </remarks>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _inner.SetScopeProvider(scopeProvider);

    public void Dispose() => _inner.Dispose();
}

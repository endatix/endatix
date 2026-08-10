namespace Endatix.Hosting.Logging;

/// <summary>
/// How often a new log file is started.
/// </summary>
/// <remarks>
/// Mirrors the underlying sink's intervals but is declared here deliberately: the rotation
/// implementation is an internal detail, and leaking its enum into this options type would put it in
/// Endatix's public API and in every consumer's using directives.
/// </remarks>
public enum FileRollingInterval
{
    /// <summary>One file, never rolled on time.</summary>
    Infinite,

    /// <summary>A new file each year.</summary>
    Year,

    /// <summary>A new file each month.</summary>
    Month,

    /// <summary>A new file each day.</summary>
    Day,

    /// <summary>A new file each hour.</summary>
    Hour,

    /// <summary>A new file each minute.</summary>
    Minute
}

/// <summary>
/// Output shape of each line in the log file.
/// </summary>
public enum FileLogFormatter
{
    /// <summary>One JSON object per record, with its structured properties intact.</summary>
    Json,

    /// <summary>A rendered, human-readable line.</summary>
    Text
}

/// <summary>
/// Options for optional rotating file logging, bound from <c>Endatix:Logging:File</c>.
/// </summary>
/// <remarks>
/// Disabled by default. File logging exists for self-hosted operators running the API as a service;
/// containers should rely on console plus OTLP, and the Helm chart's read-only root filesystem makes
/// a default-on file logger actively harmful.
/// </remarks>
public sealed class FileLoggingOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "Endatix:Logging:File";

    /// <summary>
    /// Whether log files are written. Off by default.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// File path. Relative paths resolve against the content root, not the filesystem root.
    /// </summary>
    /// <remarks>
    /// The rotation suffix is inserted before the extension, so <c>logs/endatix-.log</c> produces
    /// <c>logs/endatix-20260809.log</c>.
    /// </remarks>
    public string Path { get; set; } = "logs/endatix-.log";

    /// <summary>
    /// Output shape. Defaults to JSON, matching the sink this replaces.
    /// </summary>
    public FileLogFormatter Formatter { get; set; } = FileLogFormatter.Json;

    /// <summary>
    /// How often to start a new file.
    /// </summary>
    public FileRollingInterval RollingInterval { get; set; } = FileRollingInterval.Day;

    /// <summary>
    /// Size at which a new file is started, in bytes. Defaults to 10 MiB.
    /// </summary>
    public long FileSizeLimitBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Whether to roll to a new file on reaching <see cref="FileSizeLimitBytes"/>.
    /// </summary>
    /// <remarks>
    /// With this off, the sink stops writing once the limit is reached rather than rolling, which
    /// loses records silently.
    /// </remarks>
    public bool RollOnFileSizeLimit { get; set; } = true;

    /// <summary>
    /// How many files to keep, including the current one. Older files are deleted as new ones roll.
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 7;
}

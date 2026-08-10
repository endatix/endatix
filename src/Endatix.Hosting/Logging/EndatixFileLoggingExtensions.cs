using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Json;
using SerilogRollingInterval = Serilog.RollingInterval;

namespace Endatix.Hosting.Logging;

/// <summary>
/// Registers optional rotating file logging as a single <see cref="ILoggerProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// Serilog appears here as the rotation implementation and nowhere else. Rotation is deceptively
/// hard -- concurrent writers, retention pruning, disk-full and Windows file locking -- and is not
/// worth owning. It is registered as a plain provider, so source-generated logging, scopes and
/// OpenTelemetry trace correlation all keep working; it is a sink, not the pipeline.
/// </para>
/// <para>
/// Consumers configure <c>Endatix:Logging:File</c> and never see a <c>Serilog</c> section.
/// </para>
/// </remarks>
public static class EndatixFileLoggingExtensions
{
    /// <summary>
    /// Adds rotating file logging when <c>Endatix:Logging:File:Enabled</c> is true. A no-op otherwise.
    /// </summary>
    /// <param name="logging">The logging builder.</param>
    /// <param name="configuration">Configuration to bind <see cref="FileLoggingOptions"/> from.</param>
    /// <param name="contentRootPath">
    /// Base directory for relative paths. When omitted, the host's content root is read from
    /// configuration, falling back to the current directory only if the host did not publish one.
    /// </param>
    /// <returns>The logging builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// The configured path is not writable. Thrown at startup, by design: a log file that silently
    /// goes nowhere is worse than a host that refuses to start.
    /// </exception>
    public static ILoggingBuilder AddEndatixFileLogging(
        this ILoggingBuilder logging,
        IConfiguration configuration,
        string? contentRootPath = null)
    {
        ArgumentNullException.ThrowIfNull(logging);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = BindOptions(configuration);
        if (!options.Enabled)
        {
            return logging;
        }

        var resolvedPath = ResolvePath(options.Path, contentRootPath ?? GetContentRoot(configuration));
        EnsureWritable(resolvedPath);

        var loggerConfiguration = new LoggerConfiguration()
            // Level filtering is the logging pipeline's job, through the standard provider-scoped
            // section. Filtering again here would silently override it and make the two disagree.
            .MinimumLevel.Verbose();

        if (options.Formatter == FileLogFormatter.Json)
        {
            loggerConfiguration.WriteTo.File(
                formatter: new JsonFormatter(),
                path: resolvedPath,
                rollingInterval: ToSerilog(options.RollingInterval),
                fileSizeLimitBytes: options.FileSizeLimitBytes,
                rollOnFileSizeLimit: options.RollOnFileSizeLimit,
                retainedFileCountLimit: options.RetainedFileCountLimit);
        }
        else
        {
            loggerConfiguration.WriteTo.File(
                path: resolvedPath,
                rollingInterval: ToSerilog(options.RollingInterval),
                fileSizeLimitBytes: options.FileSizeLimitBytes,
                rollOnFileSizeLimit: options.RollOnFileSizeLimit,
                retainedFileCountLimit: options.RetainedFileCountLimit);
        }

        // dispose: true -- this logger is owned by the provider, so the sink is flushed and the file
        // handle released when the host shuts down.
        logging.AddProvider(new EndatixFileLoggerProvider(loggerConfiguration.CreateLogger(), dispose: true));

        return logging;
    }

    /// <summary>
    /// Reads the host's content root from configuration.
    /// </summary>
    /// <remarks>
    /// The generic host writes its content root into configuration under
    /// <see cref="HostDefaults.ContentRootKey"/>, so this works without widening
    /// <c>IAppEnvironment</c>. It matters wherever the content root and the working directory differ
    /// — most sharply for a Windows service, whose working directory is <c>C:\Windows\System32</c>,
    /// and which is exactly the self-hosted operator this feature exists for.
    /// </remarks>
    internal static string? GetContentRoot(IConfiguration configuration) =>
        configuration[HostDefaults.ContentRootKey];

    internal static FileLoggingOptions BindOptions(IConfiguration configuration)
    {
        var options = new FileLoggingOptions();
        configuration.GetSection(FileLoggingOptions.SectionName).Bind(options);

        return options;
    }

    /// <summary>
    /// Resolves a relative path against the content root.
    /// </summary>
    /// <remarks>
    /// The configuration this replaces wrote to <c>/logs</c> at the filesystem root, which fails on
    /// every macOS dev machine and any non-root Linux host. Anchoring relative paths to the content
    /// root is what makes the default usable without elevated permissions.
    /// </remarks>
    internal static string ResolvePath(string path, string? contentRootPath)
    {
        if (System.IO.Path.IsPathRooted(path))
        {
            return path;
        }

        var root = string.IsNullOrWhiteSpace(contentRootPath)
            ? Directory.GetCurrentDirectory()
            : contentRootPath;

        return System.IO.Path.GetFullPath(System.IO.Path.Combine(root, path));
    }

    /// <summary>
    /// Fails fast when the target directory cannot be created or written to.
    /// </summary>
    /// <remarks>
    /// The sink being replaced reported this only to Serilog's <c>SelfLog</c>, so a misconfigured
    /// path produced no log files and no complaint. Probing here converts that into a startup error
    /// naming the path.
    /// </remarks>
    private static void EnsureWritable(string resolvedPath)
    {
        var directory = System.IO.Path.GetDirectoryName(resolvedPath);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);

            // Creating the directory is not proof of write access -- it may already exist and be
            // read-only, which is exactly the read-only-root-filesystem case.
            var probe = System.IO.Path.Combine(directory, $".endatix-write-probe-{Guid.NewGuid():N}");
            using (var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
                stream.WriteByte(0);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"File logging is enabled but '{directory}' is not writable, so no log files could be "
                + $"written. Set '{FileLoggingOptions.SectionName}:Path' to a writable location, or set "
                + $"'{FileLoggingOptions.SectionName}:Enabled' to false. In a container this needs a "
                + "writable volume mounted at that path.",
                ex);
        }
    }

    private static SerilogRollingInterval ToSerilog(FileRollingInterval interval) => interval switch
    {
        FileRollingInterval.Infinite => SerilogRollingInterval.Infinite,
        FileRollingInterval.Year => SerilogRollingInterval.Year,
        FileRollingInterval.Month => SerilogRollingInterval.Month,
        FileRollingInterval.Day => SerilogRollingInterval.Day,
        FileRollingInterval.Hour => SerilogRollingInterval.Hour,
        FileRollingInterval.Minute => SerilogRollingInterval.Minute,
        _ => SerilogRollingInterval.Day
    };
}

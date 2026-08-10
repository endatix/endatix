using System.Text.Json;
using Endatix.Hosting.Builders;
using Endatix.Hosting.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Endatix.Hosting.Tests.Logging;

/// <summary>
/// Tests for optional rotating file logging: that it stays off unless asked for, actually writes and
/// rolls when enabled, never displaces the console, and refuses to start rather than writing nowhere.
/// </summary>
public sealed class EndatixFileLoggingTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"endatix-filelog-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static IConfiguration Config(params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => s.Value))
            .Build();

    private static IConfiguration EnabledAt(string path, params (string Key, string? Value)[] extra)
    {
        var settings = new List<(string, string?)>
        {
            ("Endatix:Logging:File:Enabled", "true"),
            ("Endatix:Logging:File:Path", path)
        };
        settings.AddRange(extra);

        return Config([.. settings]);
    }

    private static ILoggerFactory BuildFactory(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddEndatixFileLogging(configuration);
        });

        return services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
    }

    [Fact]
    public void AddEndatixFileLogging_WhenDisabled_RegistersNoProvider()
    {
        // Arrange
        // Off by default matters operationally: the Helm chart runs with a read-only root filesystem,
        // so a default-on file logger would crash-loop pods.
        var services = new ServiceCollection();

        // Act
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddEndatixFileLogging(Config(("Endatix:Logging:File:Path", "logs/x-.log")));
        });

        // Assert
        using var provider = services.BuildServiceProvider();
        provider.GetServices<ILoggerProvider>().Should().BeEmpty();
    }

    [Fact]
    public void AddEndatixFileLogging_WhenEnabled_WritesRecordsToTheConfiguredPath()
    {
        // Arrange
        var path = Path.Combine(_root, "logs", "endatix-.log");
        var factory = BuildFactory(EnabledAt(path));

        // Act
        factory.CreateLogger("Endatix.Tests").LogWarning("file sink reached");
        factory.Dispose();

        // Assert
        var written = Directory.GetFiles(Path.Combine(_root, "logs"));
        written.Should().NotBeEmpty();
        File.ReadAllText(written[0]).Should().Contain("file sink reached");
    }

    [Fact]
    public void AddEndatixFileLogging_WhenEnabled_DefaultsToJsonWithPropertiesIntact()
    {
        // Arrange
        // Parity with the sink this replaces, which was already configured with a JSON formatter.
        // Structured output is the point: a rendered line cannot be queried by FormId.
        var path = Path.Combine(_root, "logs", "endatix-.log");
        var factory = BuildFactory(EnabledAt(path));

        // Act
        factory.CreateLogger("Endatix.Tests").LogWarning("Form {FormId} rejected", 12345L);
        factory.Dispose();

        // Assert
        // Parsed rather than substring-matched: "12345" appears in a rendered line too, so a
        // substring check would pass even if the formatter silently fell back to plain text.
        var contents = File.ReadAllText(Directory.GetFiles(Path.Combine(_root, "logs"))[0]);
        var firstLine = contents.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0];

        using var record = JsonDocument.Parse(firstLine);
        record.RootElement
            .GetProperty("Properties")
            .GetProperty("FormId")
            .GetInt64()
            .Should().Be(12345L);
    }

    [Fact]
    public void AddEndatixFileLogging_WithHostContentRoot_ResolvesRelativePathsAgainstIt()
    {
        // Arrange
        // The content root and the working directory are not always the same. A Windows service
        // runs with a working directory of C:\Windows\System32 -- which is precisely the
        // self-hosted operator this feature exists for, so falling back to it would be wrong.
        var contentRoot = Path.Combine(_root, "content");
        Directory.CreateDirectory(contentRoot);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endatix:Logging:File:Enabled"] = "true",
                ["Endatix:Logging:File:Path"] = "logs/endatix-.log",
                [HostDefaults.ContentRootKey] = contentRoot
            })
            .Build();

        // Act
        var factory = BuildFactory(configuration);
        factory.CreateLogger("Endatix.Tests").LogWarning("under the content root");
        factory.Dispose();

        // Assert
        var underContentRoot = Path.Combine(contentRoot, "logs");
        Directory.Exists(underContentRoot).Should().BeTrue();
        Directory.GetFiles(underContentRoot).Should().NotBeEmpty();

        // And emphatically not under the process working directory.
        Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "logs", "endatix-.log"))
            .Should().BeFalse();
    }

    [Fact]
    public void FileProvider_LevelsAreConfiguredUnderEndatixFile_NotSerilog()
    {
        // Arrange
        // The provider is aliased so its level key reads Logging:EndatixFile:LogLevel. Registering
        // SerilogLoggerProvider directly would make it Logging:Serilog:LogLevel and put the rotation
        // library's name on the configuration surface, which this feature exists to keep private.
        var path = Path.Combine(_root, "logs", "endatix-.log");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endatix:Logging:File:Enabled"] = "true",
                ["Endatix:Logging:File:Path"] = path,
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:EndatixFile:LogLevel:Endatix"] = "Information"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.AddConfiguration(configuration.GetSection("Logging"));
            logging.ClearProviders();
            logging.AddEndatixFileLogging(configuration);
        });

        var factory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
        var logger = factory.CreateLogger("Endatix.Tests");

        // Act
        logger.LogInformation("INFORMATION-LIFTED-FOR-THE-FILE");
        logger.LogDebug("DEBUG-BELOW-THE-CONFIGURED-LEVEL");
        factory.Dispose();

        // Assert
        var contents = File.ReadAllText(Directory.GetFiles(Path.Combine(_root, "logs"))[0]);

        contents.Should().Contain("INFORMATION-LIFTED-FOR-THE-FILE",
            "the provider-scoped section should lift Endatix above the global Warning default");
        contents.Should().NotContain("DEBUG-BELOW-THE-CONFIGURED-LEVEL");
    }

    [Fact]
    public void FileProvider_PreservesScopeProperties()
    {
        // Arrange
        // The alias is achieved by delegating to SerilogLoggerProvider, which is sealed. Delegation
        // drops scopes unless ISupportExternalScope is forwarded, and nothing else in the pipeline
        // reports that loss -- the properties just stop appearing.
        var path = Path.Combine(_root, "logs", "endatix-.log");
        var factory = BuildFactory(EnabledAt(path));
        var logger = factory.CreateLogger("Endatix.Tests");

        // Act
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = "abc-123" }))
        {
            logger.LogWarning("inside a scope");
        }

        factory.Dispose();

        // Assert
        var contents = File.ReadAllText(Directory.GetFiles(Path.Combine(_root, "logs"))[0]);
        contents.Should().Contain("CorrelationId");
        contents.Should().Contain("abc-123");
    }

    [Fact]
    public void AddEndatixFileLogging_RollsOnSizeLimit_AndPrunesBeyondRetainedCount()
    {
        // Arrange
        // The whole reason for taking a dependency rather than writing this: rolling and pruning are
        // what a file logger is for. Asserting only that "a file exists" would pass without either.
        var path = Path.Combine(_root, "logs", "endatix-.log");
        var factory = BuildFactory(EnabledAt(
            path,
            ("Endatix:Logging:File:RollingInterval", "Infinite"),
            ("Endatix:Logging:File:FileSizeLimitBytes", "1024"),
            ("Endatix:Logging:File:RollOnFileSizeLimit", "true"),
            ("Endatix:Logging:File:RetainedFileCountLimit", "3")));

        var logger = factory.CreateLogger("Endatix.Tests");

        // Act
        // Comfortably past 3 x 1 KiB, so rolling must happen several times and pruning must engage.
        for (var i = 0; i < 400; i++)
        {
            logger.LogWarning("padding {Index} {Filler}", i, new string('x', 200));
        }

        factory.Dispose();

        // Assert
        var files = Directory.GetFiles(Path.Combine(_root, "logs"));

        files.Length.Should().BeGreaterThan(1, "the size limit should have forced a roll");
        files.Length.Should().BeLessThanOrEqualTo(3, "files beyond RetainedFileCountLimit should be pruned");
    }

    [Fact]
    public void AddEndatixFileLogging_WhenEnabled_ConsoleProviderStillRegistered()
    {
        // Arrange
        // Enabling files must not cost a deployed host its stdout -- that is what `kubectl logs`
        // reads, and losing it would be a silent operational regression.
        var path = Path.Combine(_root, "logs", "endatix-.log");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endatix:Logging:File:Enabled"] = "true",
                ["Endatix:Logging:File:Path"] = path
            })
            .Build();

        var builder = new EndatixLoggingBuilder(new ServiceCollection(), configuration);

        // Act
        builder.UseDefaults();

        // Assert
        using var provider = builder.Services.BuildServiceProvider();
        var providers = provider.GetServices<ILoggerProvider>().ToList();

        providers.Should().Contain(p => p is ConsoleLoggerProvider);
        providers.Should().Contain(p => p is EndatixFileLoggerProvider);
    }

    [Fact]
    public void AddEndatixFileLogging_WithUnusablePath_ThrowsNamingThePath()
    {
        // Arrange
        // The configuration this replaces wrote to /logs at the filesystem root and failed silently
        // on every non-root host, reporting only to Serilog's SelfLog. Failing loudly is the fix.
        //
        // Unusability is created by putting a *file* where a directory has to go, rather than by
        // clearing write permissions: root ignores permission bits, so a chmod-based test passes or
        // fails depending on which uid runs it. Tests must not depend on that -- CI containers
        // commonly run as root while developer machines do not.
        var blocker = Path.Combine(_root, "blocker");
        Directory.CreateDirectory(_root);
        File.WriteAllText(blocker, "not a directory");

        var unusableDirectory = Path.Combine(blocker, "logs");
        var configuration = EnabledAt(Path.Combine(unusableDirectory, "endatix-.log"));

        // Act
        var act = () => BuildFactory(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{unusableDirectory}*")
            .WithMessage("*Enabled*");
    }

    [Fact]
    public void ResolvePath_WithRelativePath_AnchorsToContentRootNotFilesystemRoot()
    {
        // Arrange
        // "logs/endatix-.log" must not become "/logs/endatix-.log". That single character was why the
        // previous configuration silently wrote nothing on macOS and non-root Linux.
        var contentRoot = Path.Combine(_root, "content");

        // Act
        var resolved = EndatixFileLoggingExtensions.ResolvePath("logs/endatix-.log", contentRoot);

        // Assert
        resolved.Should().Be(Path.GetFullPath(Path.Combine(contentRoot, "logs", "endatix-.log")));
        resolved.Should().NotBe("/logs/endatix-.log");
    }

    [Fact]
    public void ResolvePath_WithAbsolutePath_IsLeftAlone()
    {
        // Arrange
        var absolute = Path.Combine(_root, "explicit", "endatix-.log");

        // Act
        var resolved = EndatixFileLoggingExtensions.ResolvePath(absolute, _root);

        // Assert
        resolved.Should().Be(absolute);
    }
}

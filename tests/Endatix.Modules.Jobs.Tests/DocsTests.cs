using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.Configuration;

namespace Endatix.Modules.Jobs.Tests;

/// <summary>
/// The configuration page is the operator's reference, so an option added to the code without a line there, or a
/// default changed in one place only, is a gap an operator cannot see.
/// </summary>
public sealed partial class DocsTests
{
    private const string Section = "Endatix:BackgroundJobs";

    [Fact]
    public void Docs_ListEveryBackgroundJobsOption()
    {
        // Arrange
        var page = ReadConfigurationPage();
        var perJobTypeOnly = typeof(BackgroundJobTypeOptions).GetProperties()
            .Select(property => property.Name)
            .Where(name => typeof(BackgroundJobsOptions).GetProperty(name) is null);

        // Act
        var keys = Options(new BackgroundJobsOptions(), Section)
            .Select(option => option.Key)
            .Concat(perJobTypeOnly.Select(name => $"{Section}:JobTypes:{{JobType}}:{name}"))
            .ToList();

        // Assert — every key has its own entry with its default, and the cut-over switch is listed as off.
        keys.Should().NotBeEmpty();
        foreach (var key in keys)
        {
            page.Should().Contain($"<Setting name=\"{key}\" default=", "the page must document {0} and its default", key);
        }

        page.Should().MatchRegex(@"(?m)^\| `DeliverToJobQueue` \|[^\n]*\| `false` \|\s*$");
    }

    [Fact]
    public void Docs_StateTheDefaultTheCodeSetsForEveryBackgroundJobsOption()
    {
        // Arrange
        var documented = DocumentedDefaults();

        // Act — an option with no value in code has nothing to compare here: the generated instance id, and the
        // trigger lock, whose default depends on the provider and is checked on its own.
        var defaults = Options(new BackgroundJobsOptions(), Section)
            .Where(option => option.Default is not null)
            .ToList();

        // Assert
        defaults.Should().NotBeEmpty();
        foreach (var (key, value) in defaults)
        {
            documented.Should().Contain(key, AsDocumented(value!), "the page must state the default the code sets for {0}", key);
        }
    }

    [Theory]
    [InlineData("postgresql", "PostgreSQL")]
    [InlineData("sqlserver", "SQL Server")]
    public void Docs_StateTheTriggerLockDefaultTheStoreSetsForEachProvider(string provider, string providerName)
    {
        // Arrange
        var documented = DocumentedDefaults();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=unused",
                ["ConnectionStrings:DefaultConnection_DbProvider"] = provider,
            })
            .Build();

        // Act
        var locksOnInsert = JobStoreDatabase.From(configuration, new BackgroundJobsOptions()).LocksOnInsert;

        // Assert
        var key = $"{Section}:{nameof(BackgroundJobsOptions.LockOnInsert)}";
        documented.Should().ContainKey(key);
        documented[key].Should().Contain(
            $"{AsDocumented(locksOnInsert)} on {providerName}",
            "the page must state the trigger lock default the store sets on {0}",
            providerName);
    }

    private static Dictionary<string, string> DocumentedDefaults() =>
        SettingDefaults().Matches(ReadConfigurationPage())
            .ToDictionary(setting => setting.Groups["key"].Value, setting => setting.Groups["default"].Value);

    // Each key with the value a host that sets nothing runs with. A global left unset in code, so that a job type's
    // own default can win, falls back to its Default constant. Per-job-type overrides are listed with their global
    // keys.
    private static IEnumerable<(string Key, object? Default)> Options(object section, string prefix) =>
        section.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => !IsDictionary(property.PropertyType))
            .SelectMany(property => OptionsOf(property, property.GetValue(section), $"{prefix}:{property.Name}"));

    // A nested options class is a section of keys; anything else is one key.
    private static IEnumerable<(string Key, object? Default)> OptionsOf(PropertyInfo property, object? value, string key) =>
        IsSection(property.PropertyType) ? Options(value!, key) : [(key, value ?? GlobalDefault(property.Name))];

    private static object? GlobalDefault(string optionName) =>
        typeof(BackgroundJobsOptions).GetField($"Default{optionName}")?.GetValue(null);

    private static string AsDocumented(object value) =>
        value is bool flag ? flag.ToString().ToLowerInvariant() : Convert.ToString(value, CultureInfo.InvariantCulture)!;

    private static bool IsSection(Type type) => type.IsClass && type != typeof(string);

    private static bool IsDictionary(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>);

    [GeneratedRegex("<Setting name=\"(?<key>[^\"]+)\" default=\"(?<default>[^\"]*)\"")]
    private static partial Regex SettingDefaults();

    private static string ReadConfigurationPage() =>
        File.ReadAllText(Path.Combine(
            RepositoryRoot(), "docs", "endatix-docs", "docs", "configuration", "background-processing.mdx"));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Endatix.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}

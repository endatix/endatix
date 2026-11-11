using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Endatix.Framework.Modules;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Audience;
using Endatix.Modules.Jobs.Runtime;
using Endatix.Modules.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Jobs.Tests;

/// <summary>
/// The configuration page is the operator's reference, so an option added to the code without a line there, or a
/// default changed in one place only, is a gap an operator cannot see.
/// </summary>
public sealed partial class DocsTests
{
    private const string SECTION = "Endatix:BackgroundJobs";

    [Fact]
    public void Docs_ListEveryBackgroundJobsOption()
    {
        // Arrange
        var page = ReadConfigurationPage();

        // Act
        var keys = LeafKeys();

        var deliverToJobQueue = AsDocumented(new OutboxDeliveryOptions().DeliverToJobQueue);

        // Assert — every key has its own entry with its default, and the cut-over switch with the default the code sets.
        keys.Should().NotBeEmpty();
        foreach (var key in keys)
        {
            page.Should().Contain($"<Setting name=\"{key}\" default=", "the page must document {0} and its default", key);
        }

        page.Should().MatchRegex($@"(?m)^\| `DeliverToJobQueue` \|[^\n]*\| `{deliverToJobQueue}` \|\s*$");
    }

    [Fact]
    public void Docs_ListOnlyKeysTheOptionsBind()
    {
        // Arrange
        var keys = LeafKeys();

        // Act
        var documented = DocumentedDefaults().Keys.ToList();

        // Assert — a key renamed or removed in code must not stay on the page.
        documented.Should().NotBeEmpty();
        documented.Except(keys).Should().BeEmpty("every setting on the page must be a key the options bind");
    }

    [Fact]
    public void Docs_StateTheBuiltInDefaultsEveryJobTypeDeclaresInCode()
    {
        // Arrange
        var declared = DeclaredJobTypeDefaults();

        // Act
        var documented = DocumentedJobTypeDefaults();

        // Assert
        declared.Should().NotBeEmpty();
        documented.Keys.Should().BeEquivalentTo(
            declared.Keys, "the page must list every job type that declares defaults in code, and only those");
        foreach (var (jobType, defaults) in declared)
        {
            documented[jobType].Should().BeEquivalentTo(
                defaults, "the page must state the defaults {0} declares in code", jobType);
        }
    }

    [Fact]
    public void Docs_ReadJobTypeDefaultsFromEveryProjectThatDeclaresThem()
    {
        // Arrange
        var sourceRoot = Path.Combine(RepositoryRoot(), "src");

        // Act
        var declaring = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadLines(file).Any(line => JobTypeDefaultsRegistration().IsMatch(line)))
            .Select(file => Path.GetRelativePath(sourceRoot, file).Split(Path.DirectorySeparatorChar)[0])
            .Distinct()
            .ToList();

        // Assert — a project that registers job types this class does not read would leave them off the page,
        // which then calls them "any other job type".
        declaring.Should().NotBeEmpty();
        declaring.Should().BeSubsetOf(
            _projectsDeclaringJobTypeDefaults,
            "DeclaredJobTypeDefaults must register every project that declares job type defaults");
    }

    [Fact]
    public void Docs_StateTheConcurrencyCapTheCodeSetsForEachKindOfJobType()
    {
        // Arrange
        var page = ReadConfigurationPage();
        var declared = DeclaredJobTypeDefaults();
        var maxConcurrency = nameof(BackgroundJobTypeOptions.MaxConcurrency);
        var anyOther = BackgroundJobsOptions.DefaultJobTypeMaxConcurrency;

        // Act
        var webHook = declared["WebHookDelivery"][maxConcurrency];
        var moduleCaps = declared
            .Where(jobType => jobType.Key.StartsWith("Reporting", StringComparison.Ordinal)
                || jobType.Key.StartsWith("Audience", StringComparison.Ordinal))
            .Select(jobType => jobType.Value[maxConcurrency])
            .Distinct()
            .ToList();

        // Assert — the page gives the Reporting and Audience types one cap, so a type with another must change it.
        moduleCaps.Should().ContainSingle("the page states one cap for every Reporting and Audience type");
        page.Should().Contain($"- **Any other job type**: the general values, and `{maxConcurrency}` {anyOther}.");
        page.Should().Contain($"{webHook} for `WebHookDelivery`, {moduleCaps[0]} for each Reporting and Audience type, {anyOther} for any other.");
    }

    [Fact]
    public void Docs_StateTheDefaultTheCodeSetsForEveryBackgroundJobsOption()
    {
        // Arrange
        var documented = DocumentedDefaults();

        // Act — an option with no value in code has nothing to compare here: the generated instance id, and the
        // trigger lock, whose default depends on the provider and is checked on its own.
        var options = Options(new BackgroundJobsOptions(), SECTION).ToList();
        var defaults = options.Where(option => option.Default is not null).ToList();
        var withoutDefault = options.Where(option => option.Default is null).Select(option => option.Key);

        // Assert — a new option with no value in code must be checked on its own, not skipped here.
        withoutDefault.Should().BeEquivalentTo(
            [$"{SECTION}:{nameof(BackgroundJobsOptions.LockOnInsert)}", $"{SECTION}:Clustering:{nameof(BackgroundJobsClusteringOptions.InstanceId)}"],
            "only the provider-dependent trigger lock and the generated instance id have no value in code");
        defaults.Should().NotBeEmpty();
        foreach (var (key, value) in defaults)
        {
            documented.Should().Contain(key, AsDocumented(value!), "the page must state the default the code sets for {0}", key);
        }
    }

    [Fact]
    public void Readme_StateTheDefaultTheCodeSetsForEveryBackgroundJobsOption()
    {
        // Arrange
        var documented = ReadmeDefaults();

        // Act
        var defaults = Options(new BackgroundJobsOptions(), SECTION)
            .Where(option => option.Default is not null)
            .ToList();

        // Assert
        defaults.Should().NotBeEmpty();
        foreach (var (key, value) in defaults)
        {
            documented.Should().Contain(key, AsDocumented(value!), "the README must state the default the code sets for {0}", key);
        }
    }

    [Fact]
    public void Readme_StateTheBuiltInDefaultsEveryJobTypeDeclaresInCode()
    {
        // Arrange
        var declared = DeclaredJobTypeDefaults();

        // Act
        var documented = ReadmeJobTypeDefaults();

        // Assert — "any other" is the row for every job type that declares nothing.
        var maxConcurrency = nameof(BackgroundJobTypeOptions.MaxConcurrency);
        documented.Should().ContainKey(ANY_OTHER_JOB_TYPE);
        documented[ANY_OTHER_JOB_TYPE].Should().BeEquivalentTo(
            new Dictionary<string, int> { [maxConcurrency] = BackgroundJobsOptions.DefaultJobTypeMaxConcurrency });
        documented.Remove(ANY_OTHER_JOB_TYPE);
        documented.Keys.Should().BeEquivalentTo(
            declared.Keys, "the README must list every job type that declares defaults in code, and only those");
        foreach (var (jobType, defaults) in declared)
        {
            documented[jobType].Should().BeEquivalentTo(
                defaults, "the README must state the defaults {0} declares in code", jobType);
        }
    }

    [Theory]
    [InlineData("postgresql", "PostgreSQL")]
    [InlineData("sqlserver", "SQL Server")]
    public void Docs_StateTheTriggerLockDefaultTheStoreSetsForEachProvider(string provider, string providerName)
    {
        // Arrange
        var documented = DocumentedDefaults();
        var configuration = ProviderConfiguration(provider);

        // Act
        var locksOnInsert = JobStoreDatabase.From(configuration, new BackgroundJobsOptions()).LocksOnInsert;

        // Assert
        var key = $"{SECTION}:{nameof(BackgroundJobsOptions.LockOnInsert)}";
        documented.Should().ContainKey(key);
        documented[key].Should().Contain(
            $"{AsDocumented(locksOnInsert)} on {providerName}",
            "the page must state the trigger lock default the store sets on {0}",
            providerName);
    }

    // Every key the options bind, a job type's own settings under the JobTypes placeholder.
    private static List<string> LeafKeys()
    {
        var perJobTypeOnly = typeof(BackgroundJobTypeOptions).GetProperties()
            .Select(property => property.Name)
            .Where(name => typeof(BackgroundJobsOptions).GetProperty(name) is null);

        return Options(new BackgroundJobsOptions(), SECTION)
            .Select(option => option.Key)
            .Concat(perJobTypeOnly.Select(name => $"{SECTION}:JobTypes:{{JobType}}:{name}"))
            .ToList();
    }

    // The src/ projects whose registrations DeclaredJobTypeDefaults runs.
    private static readonly string[] _projectsDeclaringJobTypeDefaults =
        ["Endatix.Infrastructure", "Endatix.Modules.Reporting", "Endatix.Modules.Audience"];

    // The registrations that declare job type defaults: webhook delivery, and each module's outbox work. Registering
    // them only describes services, so nothing here needs a database.
    private static Dictionary<string, Dictionary<string, int>> DeclaredJobTypeDefaults()
    {
        var services = new ServiceCollection();
        var modules = new EndatixModuleBuilder(services, ProviderConfiguration("postgresql"));
        services.AddWebHookProcessing();
        ReportingModule.Instance.ConfigureServices(modules);
        AudienceModule.Instance.ConfigureServices(modules);

        return services
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<BackgroundJobTypeDefaults>()
            .ToDictionary(defaults => defaults.JobType, DeclaredValues);
    }

    private static Dictionary<string, int> DeclaredValues(BackgroundJobTypeDefaults defaults) =>
        typeof(BackgroundJobTypeDefaults).GetProperties()
            .Select(property => (property.Name, Value: property.GetValue(defaults)))
            .Where(setting => setting.Value is int)
            .ToDictionary(setting => setting.Name, setting => Convert.ToInt32(setting.Value, CultureInfo.InvariantCulture));

    // Each bullet names its job types in backticks before its first setting, then states each setting as `Name` value.
    private static Dictionary<string, Dictionary<string, int>> DocumentedJobTypeDefaults()
    {
        var page = ReadConfigurationPage();
        var start = page.IndexOf("### Built-in job type defaults", StringComparison.Ordinal);
        var section = page[start..page.IndexOf(":::", start, StringComparison.Ordinal)];

        return JobTypeDefaultsLine().Matches(section)
            .SelectMany(line => BacktickedNames().Matches(line.Groups["types"].Value)
                .Select(jobType => (JobType: jobType.Groups["name"].Value, Values: DocumentedValues(line.Groups["values"].Value))))
            .ToDictionary(entry => entry.JobType, entry => entry.Values);
    }

    private static Dictionary<string, int> DocumentedValues(string settings) =>
        DocumentedSetting().Matches(settings).ToDictionary(
            setting => setting.Groups["name"].Value,
            setting => int.Parse(setting.Groups["value"].Value, CultureInfo.InvariantCulture));

    private const string ANY_OTHER_JOB_TYPE = "any other";

    // The README's job type table: the job types in backticks, then MaxAttempts, MaxRuntimeMinutes,
    // BackoffBaseSeconds / BackoffCapSeconds, RetentionDays and MaxConcurrency; "global" is a value the type does not
    // declare, and a backoff cell of "global" alone stands for both.
    private static Dictionary<string, Dictionary<string, int>> ReadmeJobTypeDefaults()
    {
        string[] columns =
        [
            nameof(BackgroundJobTypeOptions.MaxAttempts),
            nameof(BackgroundJobTypeOptions.MaxRuntimeMinutes),
            nameof(BackgroundJobTypeOptions.BackoffBaseSeconds),
            nameof(BackgroundJobTypeOptions.BackoffCapSeconds),
            nameof(BackgroundJobTypeOptions.RetentionDays),
            nameof(BackgroundJobTypeOptions.MaxConcurrency),
        ];

        return ReadmeTable("| Job type |")
            .SelectMany(cells =>
            {
                var backoff = cells[3] == "global" ? ["global", "global"] : cells[3].Split(" / ");
                string[] values = [cells[1], cells[2], .. backoff, cells[4], cells[5]];
                var declared = columns.Zip(values)
                    .Where(setting => setting.Second != "global")
                    .ToDictionary(setting => setting.First, setting => int.Parse(setting.Second, CultureInfo.InvariantCulture));
                var jobTypes = cells[0] == ANY_OTHER_JOB_TYPE
                    ? [ANY_OTHER_JOB_TYPE]
                    : BacktickedNames().Matches(cells[0]).Select(name => name.Groups["name"].Value);
                return jobTypes.Select(jobType => (JobType: jobType, Values: declared));
            })
            .ToDictionary(entry => entry.JobType, entry => entry.Values);
    }

    // The README's key table: one row may list several keys and their defaults, separated by " / ", and a key after
    // the first without a section of its own is in the first key's section.
    private static Dictionary<string, string> ReadmeDefaults() =>
        ReadmeTable("| Key |")
            .SelectMany(cells =>
            {
                var keys = BacktickedValues().Matches(cells[0]).Select(key => key.Groups["value"].Value).ToList();
                var defaults = BacktickedValues().Matches(cells[1]).Select(value => value.Groups["value"].Value).ToList();
                var section = keys[0].Contains(':') ? keys[0][..(keys[0].LastIndexOf(':') + 1)] : string.Empty;
                return keys.Count != defaults.Count
                    ? []
                    : keys.Select(key => key.Contains(':') ? key : section + key)
                        .Zip(defaults, (key, value) => (Key: $"{SECTION}:{key}", Value: value));
            })
            .ToDictionary(entry => entry.Key, entry => entry.Value);

    // The body rows of the README table whose header starts with header, each split into its trimmed cells.
    private static IEnumerable<string[]> ReadmeTable(string header) =>
        File.ReadLines(Path.Combine(RepositoryRoot(), "src", "Endatix.Modules.Jobs", "README.md"))
            .SkipWhile(line => !line.StartsWith(header, StringComparison.Ordinal))
            .Skip(2)
            .TakeWhile(line => line.StartsWith('|'))
            .Select(line => line.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray());

    private static IConfiguration ProviderConfiguration(string provider) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=unused",
                ["ConnectionStrings:DefaultConnection_DbProvider"] = provider,
            })
            .Build();

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

    // A call, not a declaration or a doc comment: every way code declares a job type's defaults goes through these.
    [GeneratedRegex(@"^(?!\s*//).*\.(AddBackgroundJobHandler<|AddBackgroundJobTypeDefaults\(|AddOutboxWork<)")]
    private static partial Regex JobTypeDefaultsRegistration();

    [GeneratedRegex("<Setting name=\"(?<key>[^\"]+)\" default=\"(?<default>[^\"]*)\"")]
    private static partial Regex SettingDefaults();

    [GeneratedRegex("(?m)^- (?<types>.+?)(?<values>`MaxAttempts` .+)$")]
    private static partial Regex JobTypeDefaultsLine();

    [GeneratedRegex("`(?<name>\\w+)`")]
    private static partial Regex BacktickedNames();

    [GeneratedRegex("`(?<value>[^`]+)`")]
    private static partial Regex BacktickedValues();

    [GeneratedRegex("`(?<name>\\w+)` (?<value>\\d+)")]
    private static partial Regex DocumentedSetting();

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

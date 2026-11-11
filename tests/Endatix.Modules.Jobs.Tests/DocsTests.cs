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
    private const string Section = "Endatix:BackgroundJobs";

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
            ProjectsDeclaringJobTypeDefaults,
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
        var options = Options(new BackgroundJobsOptions(), Section).ToList();
        var defaults = options.Where(option => option.Default is not null).ToList();
        var withoutDefault = options.Where(option => option.Default is null).Select(option => option.Key);

        // Assert — a new option with no value in code must be checked on its own, not skipped here.
        withoutDefault.Should().BeEquivalentTo(
            [$"{Section}:{nameof(BackgroundJobsOptions.LockOnInsert)}", $"{Section}:Clustering:{nameof(BackgroundJobsClusteringOptions.InstanceId)}"],
            "only the provider-dependent trigger lock and the generated instance id have no value in code");
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
        var configuration = ProviderConfiguration(provider);

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

    // Every key the options bind, a job type's own settings under the JobTypes placeholder.
    private static List<string> LeafKeys()
    {
        var perJobTypeOnly = typeof(BackgroundJobTypeOptions).GetProperties()
            .Select(property => property.Name)
            .Where(name => typeof(BackgroundJobsOptions).GetProperty(name) is null);

        return Options(new BackgroundJobsOptions(), Section)
            .Select(option => option.Key)
            .Concat(perJobTypeOnly.Select(name => $"{Section}:JobTypes:{{JobType}}:{name}"))
            .ToList();
    }

    // The src/ projects whose registrations DeclaredJobTypeDefaults runs.
    private static readonly string[] ProjectsDeclaringJobTypeDefaults =
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

using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.Infrastructure.Tests.Data;

/// <summary>
/// Committed model snapshots and migration designers keep the model they were scaffolded from. A
/// <c>ValueGenerationStrategy</c> annotation of the other EF provider in one of them compiles only while the
/// project also references that provider's EF package, and <c>has-pending-model-changes</c> cannot see it.
/// </summary>
public sealed partial class CommittedModelProviderAnnotationsTests
{
    private const string StrategyAnnotationSuffix = ":ValueGenerationStrategy";
    private const string NpgsqlPrefix = "Npgsql";
    private const string SqlServerPrefix = "SqlServer";

    private static readonly Lazy<IReadOnlyList<Type>> CommittedModelTypes = new(FindCommittedModelTypes);

    [Fact]
    public void CommittedModels_EachProvider_CarryNoStrategyAnnotationOfTheOtherProvider()
    {
        // Arrange
        var models = CommittedModelTypes.Value;

        // Act
        var violations = string.Join(Environment.NewLine, models.SelectMany(ProviderViolationsOf));

        // Assert
        models.Should().NotBeEmpty();
        violations.Should().BeEmpty();
    }

    [Fact]
    public void CommittedModels_InSourceTree_AreAllCheckedByTheProviderGuard()
    {
        // Arrange
        // Counted, because both provider assemblies hold files with the same name.
        var checkedCounts = CommittedModelTypes.Value.CountBy(CommittedFileNameOf).ToDictionary();

        // Act
        var missedFiles = string.Join(
            Environment.NewLine,
            CommittedModelFileNamesInSourceTree()
                .CountBy(name => name)
                .Where(source => checkedCounts.GetValueOrDefault(source.Key) < source.Value)
                .Select(source => source.Key));

        // Assert
        missedFiles.Should().BeEmpty("the test project must reference every assembly that holds migrations");
    }

    private static IEnumerable<string> ProviderViolationsOf(Type modelType)
    {
        var model = ModelOf(modelType);
        var provider = ProviderOf(model);
        if (provider is null)
        {
            return [$"{modelType.FullName}: no single provider strategy annotation on the model"];
        }

        var foreignAnnotation = (provider == NpgsqlPrefix ? SqlServerPrefix : NpgsqlPrefix) + StrategyAnnotationSuffix;
        return model.GetEntityTypes()
            .SelectMany(PropertiesOf)
            .Where(property => property.FindAnnotation(foreignAnnotation) is not null)
            .Select(property => $"{modelType.FullName}: {property.DeclaringType.Name}.{property.Name} has {foreignAnnotation}");
    }

    private static IEnumerable<IReadOnlyProperty> PropertiesOf(IReadOnlyTypeBase type) =>
        type.GetProperties().Concat(type.GetComplexProperties().SelectMany(complex => PropertiesOf(complex.ComplexType)));

    // The provider's identity convention writes its own strategy annotation on the model itself, in every
    // snapshot and designer, so it names the provider without relying on folder or namespace conventions.
    private static string? ProviderOf(IReadOnlyModel model)
    {
        var providers = new[] { NpgsqlPrefix, SqlServerPrefix }
            .Where(prefix => model.FindAnnotation(prefix + StrategyAnnotationSuffix) is not null)
            .ToList();

        return providers.Count == 1 ? providers[0] : null;
    }

    private static IReadOnlyModel ModelOf(Type modelType) =>
        Activator.CreateInstance(modelType) switch
        {
            ModelSnapshot snapshot => snapshot.Model,
            Migration migration => migration.TargetModel,
            _ => throw new InvalidOperationException($"{modelType.FullName} is not a snapshot or a migration."),
        };

    private static IReadOnlyList<Type> FindCommittedModelTypes() =>
        Directory.EnumerateFiles(AppContext.BaseDirectory, "Endatix.*.dll")
            .Select(path => Assembly.Load(AssemblyName.GetAssemblyName(path)))
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsCommittedModel)
            .ToList();

    private static bool IsCommittedModel(Type type) =>
        !type.IsAbstract
        && (type.IsSubclassOf(typeof(ModelSnapshot))
            || (type.IsSubclassOf(typeof(Migration)) && type.GetCustomAttribute<MigrationAttribute>() is not null));

    // EF names a designer file after the migration id and a snapshot file after the snapshot class.
    private static string CommittedFileNameOf(Type modelType) =>
        modelType.GetCustomAttribute<MigrationAttribute>() is { } migration
            ? $"{migration.Id}.Designer.cs"
            : $"{modelType.Name}.cs";

    private static IEnumerable<string> CommittedModelFileNamesInSourceTree() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => DesignerFileName().IsMatch(name) || name.EndsWith("ModelSnapshot.cs", StringComparison.Ordinal));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Endatix.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Endatix.slnx not found above the test output.");
    }

    [GeneratedRegex(@"^\d{14}_\w+\.Designer\.cs$")]
    private static partial Regex DesignerFileName();
}

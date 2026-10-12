using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.Infrastructure.Tests.Data;

/// <summary>
/// Finds the committed model snapshots and migration designers in every Endatix assembly of the test output.
/// </summary>
internal static class CommittedModels
{
    private static readonly Lazy<IReadOnlyList<Type>> AllTypes = new(FindTypes);

    /// <summary>
    /// Every model snapshot, and every migration whose designer builds a target model.
    /// </summary>
    internal static IReadOnlyList<Type> Types => AllTypes.Value;

    /// <summary>
    /// The repository's <c>src</c> folder, where the committed files live.
    /// </summary>
    internal static string SourceDirectory => Path.Combine(RepositoryRoot(), "src");

    private static IReadOnlyList<Type> FindTypes() =>
        Directory.EnumerateFiles(AppContext.BaseDirectory, "Endatix.*.dll")
            .Select(path => Assembly.Load(AssemblyName.GetAssemblyName(path)))
            .SelectMany(TypesOf)
            .Where(IsCommittedModel)
            .ToList();

    private static Type[] TypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            var loaderErrors = exception.LoaderExceptions
                .OfType<Exception>()
                .Select(error => error.Message)
                .Distinct();
            throw new InvalidOperationException(
                $"Cannot load the types of {assembly.GetName().Name}:{Environment.NewLine}"
                + string.Join(Environment.NewLine, loaderErrors),
                exception);
        }
    }

    internal static bool IsCommittedModel(Type type) =>
        !type.IsAbstract
        && (type.IsSubclassOf(typeof(ModelSnapshot)) || IsMigrationWithDesigner(type));

    // A migration without a designer has no target model, so there is no committed model to check.
    private static bool IsMigrationWithDesigner(Type type) =>
        type.IsSubclassOf(typeof(Migration))
        && type.GetCustomAttribute<MigrationAttribute>() is not null
        && type.GetMethod("BuildTargetModel", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(ModelBuilder)])
            ?.DeclaringType != typeof(Migration);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Endatix.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Endatix.slnx not found above the test output.");
    }
}

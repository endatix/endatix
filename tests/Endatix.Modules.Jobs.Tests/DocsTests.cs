using System.Reflection;
using Endatix.Modules.Jobs.Runtime;

namespace Endatix.Modules.Jobs.Tests;

/// <summary>
/// The configuration page is the operator's reference, so an option added to the code without a line there is a
/// gap an operator cannot see.
/// </summary>
public sealed class DocsTests
{
    private const string Section = "Endatix:BackgroundJobs";

    [Fact]
    public void Docs_ListEveryBackgroundJobsOption()
    {
        // Arrange
        var page = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "docs", "endatix-docs", "docs", "configuration", "background-processing.mdx"));
        var perJobTypeOnly = typeof(BackgroundJobTypeOptions).GetProperties()
            .Select(property => property.Name)
            .Where(name => typeof(BackgroundJobsOptions).GetProperty(name) is null);

        // Act
        var keys = LeafKeys(typeof(BackgroundJobsOptions), Section)
            .Concat(perJobTypeOnly.Select(name => $"{Section}:JobTypes:{{JobType}}:{name}"))
            .ToList();

        // Assert — every key has its own entry with its default, and the cut-over switch is listed as off.
        keys.Should().NotBeEmpty();
        foreach (var key in keys)
        {
            page.Should().Contain($"<Setting name=\"{key}\" default=", "the page must document {0} and its default", key);
        }

        page.Should().Contain("| `DeliverToJobQueue` |").And.Contain("| `false` |");
    }

    private static IEnumerable<string> LeafKeys(Type optionsType, string prefix)
    {
        foreach (var property in optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var key = $"{prefix}:{property.Name}";
            var type = property.PropertyType;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                // Per-job-type overrides are listed with their global keys.
                continue;
            }

            if (type.IsClass && type != typeof(string))
            {
                foreach (var nested in LeafKeys(type, key))
                {
                    yield return nested;
                }

                continue;
            }

            yield return key;
        }
    }

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

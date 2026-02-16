using System.Reflection;
using System.Text.Json;

namespace Endatix.Infrastructure.Data.SeedData;

/// <summary>
/// Reads form seed data from embedded JSON resources in the Endatix.Infrastructure assembly.
/// JSON files must be placed in Data/SeedData/Forms/ and declared as EmbeddedResource in the csproj.
/// </summary>
internal static class SeedDataReader
{
    private static readonly Assembly _assembly = typeof(SeedDataReader).Assembly;
    private static readonly string _prefix = $"{_assembly.GetName().Name}.Data.SeedData.Forms.";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Loads all form seed data files, ordered alphabetically by resource name.
    /// </summary>
    public static IEnumerable<FormSeedData> LoadAll()
    {
        var resourceNames = _assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(_prefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n);

        foreach (var name in resourceNames)
        {
            yield return Load(name);
        }
    }

    private static FormSeedData Load(string resourceName)
    {
        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Seed data resource not found: {resourceName}");

        return JsonSerializer.Deserialize<FormSeedData>(stream, _jsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize seed data from: {resourceName}");
    }
}

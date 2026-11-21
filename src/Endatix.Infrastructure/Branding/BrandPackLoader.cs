using System.Text.Json;
using Endatix.Core.Branding;
using Endatix.Framework.Modules;
using Microsoft.Extensions.Configuration;

namespace Endatix.Infrastructure.Branding;

/// <summary>
/// Resolves the <see cref="DeploymentBrand"/> at startup: the built-in brand unless the white-labeling flag is on
/// and <c>ENDATIX_BRAND_DIR</c> points at a pack. The fields the API reads are checked by
/// <see cref="BrandPackReader"/>, and an invalid pack throws, naming each failing field path.
/// </summary>
internal static class BrandPackLoader
{
    /// <summary>The configuration key (an environment variable in a pod) holding the pack folder.</summary>
    internal const string BrandDirectoryKey = "ENDATIX_BRAND_DIR";

    private const string PackFileName = "brand.json";

    /// <param name="configuration">The host configuration holding the flag and the pack folder.</param>
    /// <param name="warnings">Receives a line for each pack field the API cannot use and replaces with its built-in value.</param>
    internal static DeploymentBrand Load(IConfiguration configuration, ICollection<string>? warnings = null)
    {
        var directory = configuration[BrandDirectoryKey];
        if (!EndatixModuleRegistration.IsFeatureFlagEnabled(configuration, Framework.FeatureFlags.FeatureFlags.WhiteLabeling)
            || string.IsNullOrWhiteSpace(directory))
        {
            return DeploymentBrand.BuiltIn;
        }

        var path = Path.Combine(directory, PackFileName);
        using var document = Parse(path);
        return BrandPackReader.Read(path, document.RootElement, warnings ?? []);
    }

    private static JsonDocument Parse(string path)
    {
        if (!File.Exists(path))
        {
            throw new BrandPackException($"Brand pack file '{path}' was not found. Set {BrandDirectoryKey} to a folder that holds {PackFileName}.");
        }

        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new BrandPackException($"Brand pack file '{path}' is not valid JSON (line {ex.LineNumber + 1}).", ex);
        }
    }
}

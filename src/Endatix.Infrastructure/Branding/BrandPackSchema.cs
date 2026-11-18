using System.Reflection;
using Json.Schema;

namespace Endatix.Infrastructure.Branding;

/// <summary>
/// The embedded copy of the v1 brand pack JSON Schema. The Hub owns the canonical schema: this file is a byte copy
/// of endatix-hub's generated <c>lib/branding/brand.schema.json</c>, and <see cref="Sha256"/> pins it.
/// </summary>
internal static class BrandPackSchema
{
    private const string ResourceName = "Endatix.Infrastructure.Branding.brand.schema.json";

    /// <summary>
    /// SHA-256 (lower-case hex) of <c>brand.schema.json</c>'s UTF-8 bytes with LF line endings. A schema change
    /// copies the Hub's file and updates this value in the same change.
    /// </summary>
    internal const string Sha256 = "f6d6355e4c8b98d249ef660f8419d26bbc4c9d46f36cfe824c2eaf494daf6507";

    private static readonly Lazy<JsonSchema> Schema = new(() => JsonSchema.FromText(ReadText()));

    /// <summary>The compiled schema; built once, because building registers its <c>$id</c>.</summary>
    internal static JsonSchema Instance => Schema.Value;

    internal static string ReadText()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

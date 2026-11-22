using System.Reflection;

namespace Endatix.Infrastructure.Branding;

/// <summary>
/// The embedded copy of the v1 brand pack JSON Schema. The Hub owns the canonical schema and validates the whole pack:
/// this file is a byte copy of endatix-hub's generated <c>lib/branding/brand.schema.json</c>, and <see cref="Sha256"/>
/// pins it. The API checks only the fields it reads (<see cref="BrandPackReader"/>), so it needs no schema library.
/// </summary>
internal static class BrandPackSchema
{
    private const string ResourceName = "Endatix.Infrastructure.Branding.brand.schema.json";

    /// <summary>
    /// SHA-256 (lower-case hex) of <c>brand.schema.json</c>'s UTF-8 bytes with LF line endings. A schema change
    /// copies the Hub's file and updates this value in the same change.
    /// </summary>
    internal const string Sha256 = "e37bad9acb0ddcbaf61b1faec7fe64dea30360a97c5c29aa117950be95dee84b";

    internal static string ReadText()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

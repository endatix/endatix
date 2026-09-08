using System.Reflection;

namespace Endatix.Hosting.DevTools;

/// <summary>
/// Embedded markup, styles and script for the embed host page. Read once, inlined into the
/// response so the page stays a single request with no static-file route.
/// </summary>
internal static class EmbedHostAssets
{
    private const string Prefix = "Endatix.Hosting.DevTools.Assets.";

    public static string BuilderHtml { get; } = Read("embed-host.html");

    public static string BareHtml { get; } = Read("embed-host-bare.html");

    public static string Styles { get; } = Read("embed-host.css");

    public static string Script { get; } = Read("embed-host.js");

    private static string Read(string fileName)
    {
        var resourceName = Prefix + fileName;
        var assembly = typeof(EmbedHostAssets).GetTypeInfo().Assembly;

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded DevTools asset '{resourceName}' is missing. " +
                "Check the EmbeddedResource glob in Endatix.Hosting.csproj.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Branding;

/// <summary>
/// Checks a tenant brand override and lists every problem. The shape is the brand pack's (v1) <c>assets</c> and
/// <c>tokens</c> groups, limited to the logo, the icon and the accent; every other field is unknown.
/// </summary>
internal sealed partial class BrandOverrideReader
{
    internal const int SupportedVersion = 1;
    internal const int AssetKeyMaxLength = 256;

    private static readonly string[] RasterExtensions = [".png", ".webp"];

    /// <summary>The brand pack schema's <c>packFile</c> pattern: plain names, none empty or starting with '.', no "..".</summary>
    [GeneratedRegex(@"^(?!.*\.\.)[A-Za-z0-9_-][A-Za-z0-9._-]*(?:/[A-Za-z0-9_-][A-Za-z0-9._-]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex FilePath();

    private readonly long _tenantId;
    private readonly List<ValidationError> _problems = [];

    private BrandOverrideReader(long tenantId)
    {
        _tenantId = tenantId;
    }

    internal static IReadOnlyList<ValidationError> Check(JsonElement root, long tenantId)
    {
        var reader = new BrandOverrideReader(tenantId);
        if (reader.IsObject("", root))
        {
            reader.CheckRoot(root);
        }

        return reader._problems;
    }

    private void CheckRoot(JsonElement root)
    {
        OnlyFields("", root, "version", "assets", "tokens");
        CheckVersion(root);
        if (Optional("/assets", root, "assets") is { } assets)
        {
            CheckAssets(assets);
        }

        if (Optional("/tokens", root, "tokens") is { } tokens)
        {
            CheckTokens(tokens);
        }
    }

    private void CheckVersion(JsonElement root)
    {
        if (!root.TryGetProperty("version", out var version))
        {
            Add("/version", BrandOverrideProblems.REQUIRED, "Is required.");
        }
        else if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var value) || value != SupportedVersion)
        {
            Add("/version", BrandOverrideProblems.UNSUPPORTED_VERSION, $"Must be {SupportedVersion}.");
        }
    }

    private void CheckAssets(JsonElement assets)
    {
        OnlyFields("/assets", assets, "logo", "icon");
        if (Optional("/assets/logo", assets, "logo") is { } logo)
        {
            OnlyFields("/assets/logo", logo, "light", "dark");
            if (Required("/assets/logo/light", logo, "light") is { } light)
            {
                CheckAssetKey("/assets/logo/light", light);
            }

            if (logo.TryGetProperty("dark", out var dark))
            {
                CheckAssetKey("/assets/logo/dark", dark);
            }
        }

        if (assets.TryGetProperty("icon", out var icon))
        {
            CheckAssetKey("/assets/icon", icon);
        }
    }

    private void CheckTokens(JsonElement tokens)
    {
        OnlyFields("/tokens", tokens, "color");
        if (Optional("/tokens/color", tokens, "color") is not { } color)
        {
            return;
        }

        OnlyFields("/tokens/color", color, BrandContrast.PrimaryField, BrandContrast.PrimaryOnDarkField);
        var primaryPath = $"/tokens/color/{BrandContrast.PrimaryField}";
        var primary = ColorToken(primaryPath, Required(primaryPath, color, BrandContrast.PrimaryField));
        var hasOnDark = color.TryGetProperty(BrandContrast.PrimaryOnDarkField, out var onDarkToken);
        var onDark = hasOnDark ? ColorToken($"/tokens/color/{BrandContrast.PrimaryOnDarkField}", onDarkToken) : null;
        if (primary is { } primaryRgb && (!hasOnDark || onDark is not null))
        {
            CheckContrast(primaryRgb, onDark);
        }
    }

    private Rgb? ColorToken(string path, JsonElement? token)
    {
        if (token is not { } value || !IsObject(path, value))
        {
            return null;
        }

        OnlyFields(path, value, "$type", "$value");
        if (Required($"{path}/$type", value, "$type") is { } type && (type.ValueKind != JsonValueKind.String || type.GetString() != "color"))
        {
            Add($"{path}/$type", BrandOverrideProblems.INVALID_TYPE, "Must be \"color\".");
        }

        if (Required($"{path}/$value", value, "$value") is not { } colorValue)
        {
            return null;
        }

        var rgb = colorValue.ValueKind == JsonValueKind.String ? BrandColor.Parse(colorValue.GetString()!) : null;
        if (rgb is null)
        {
            Add($"{path}/$value", BrandOverrideProblems.INVALID_COLOR, "Use a hex, rgb(), hsl() or oklch() colour.");
        }

        return rgb;
    }

    private void CheckContrast(Rgb primary, Rgb? primaryOnDark)
    {
        foreach (var finding in BrandContrast.Check(primary, primaryOnDark))
        {
            Add(
                $"/tokens/color/{finding.Field}/$value",
                BrandOverrideProblems.LOW_CONTRAST,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The accent's contrast on the {finding.Mode} page background is {finding.Ratio:0.00}:1; it must be at least {finding.Minimum:0.##}:1."));
        }
    }

    private void CheckAssetKey(string path, JsonElement key)
    {
        if (key.ValueKind != JsonValueKind.String)
        {
            Add(path, BrandOverrideProblems.INVALID_TYPE, "Must be a string.");
            return;
        }

        var value = key.GetString()!;
        var folder = $"t/{_tenantId}/brand/";
        if (value.Length > AssetKeyMaxLength || !value.StartsWith(folder, StringComparison.Ordinal) || !FilePath().IsMatch(value[folder.Length..]))
        {
            Add(path, BrandOverrideProblems.INVALID_ASSET_KEY, $"Must be a file under '{folder}', at most {AssetKeyMaxLength} characters.");
        }
        else if (!RasterExtensions.Any(extension => value.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        {
            Add(path, BrandOverrideProblems.UNSUPPORTED_ASSET_TYPE, "Must be a PNG or WebP file.");
        }
    }

    private JsonElement? Optional(string path, JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && IsObject(path, value) ? value : null;

    private JsonElement? Required(string path, JsonElement parent, string name)
    {
        if (parent.TryGetProperty(name, out var value))
        {
            return value;
        }

        Add(path, BrandOverrideProblems.REQUIRED, "Is required.");
        return null;
    }

    private bool IsObject(string path, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        Add(path, BrandOverrideProblems.INVALID_TYPE, "Must be a JSON object.");
        return false;
    }

    private void OnlyFields(string path, JsonElement value, params string[] allowed)
    {
        foreach (var property in value.EnumerateObject().Where(property => !allowed.Contains(property.Name, StringComparer.Ordinal)))
        {
            Add($"{path}/{Escape(property.Name)}", BrandOverrideProblems.UNKNOWN_FIELD, "Is not a field a tenant can set.");
        }
    }

    /// <summary>JSON Pointer escaping (RFC 6901).</summary>
    private static string Escape(string name) => name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private void Add(string path, string code, string detail) =>
        _problems.Add(new ValidationError(path, detail, code, ValidationSeverity.Error));
}

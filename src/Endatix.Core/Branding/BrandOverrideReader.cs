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
    internal const int SUPPORTED_VERSION = 1;
    internal const int ASSET_KEY_MAX_LENGTH = 256;

    /// <summary>Longer than any colour the Hub writes, so the colour patterns never scan long runs of spaces or digits.</summary>
    internal const int COLOR_MAX_LENGTH = 64;

    /// <summary>Unknown fields reported, at most; the rest are left out of the problem list.</summary>
    internal const int MAX_UNKNOWN_FIELDS = 10;

    private static readonly string[] _rasterExtensions = [".png", ".webp"];

    /// <summary>The brand pack schema's <c>packFile</c> pattern: plain names, none empty or starting with '.', no "..".</summary>
    internal const string FILE_PATH_PATTERN = @"^(?!.*\.\.)[A-Za-z0-9_-][A-Za-z0-9._-]*(?:/[A-Za-z0-9_-][A-Za-z0-9._-]*)*$";

    [GeneratedRegex(FILE_PATH_PATTERN, RegexOptions.CultureInvariant)]
    private static partial Regex FilePath();

    private readonly long _tenantId;
    private readonly List<ValidationError> _problems = [];
    private int _unknownFields;

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
        else if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var value) || value != SUPPORTED_VERSION)
        {
            Add("/version", BrandOverrideProblems.UNSUPPORTED_VERSION, $"Must be {SUPPORTED_VERSION}.");
        }
    }

    private void CheckAssets(JsonElement assets)
    {
        OnlyFields("/assets", assets, "logo", "icon");
        if (Optional("/assets/logo", assets, "logo") is { } logo)
        {
            CheckLogo(logo);
        }

        if (assets.TryGetProperty("icon", out var icon))
        {
            CheckAssetKey("/assets/icon", icon);
        }
    }

    private void CheckLogo(JsonElement logo)
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

    private void CheckTokens(JsonElement tokens)
    {
        OnlyFields("/tokens", tokens, "color");
        if (Optional("/tokens/color", tokens, "color") is not { } color)
        {
            return;
        }

        OnlyFields("/tokens/color", color, BrandContrast.PRIMARY_FIELD, BrandContrast.PRIMARY_ON_DARK_FIELD);
        var primaryPath = $"/tokens/color/{BrandContrast.PRIMARY_FIELD}";
        var primary = ColorToken(primaryPath, Required(primaryPath, color, BrandContrast.PRIMARY_FIELD));
        var hasOnDark = color.TryGetProperty(BrandContrast.PRIMARY_ON_DARK_FIELD, out var onDarkToken);
        var onDark = hasOnDark ? ColorToken($"/tokens/color/{BrandContrast.PRIMARY_ON_DARK_FIELD}", onDarkToken) : null;
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

        return Required($"{path}/$value", value, "$value") is { } colorValue ? Color($"{path}/$value", colorValue) : null;
    }

    private Rgb? Color(string path, JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()! : null;
        var rgb = text?.Length <= COLOR_MAX_LENGTH ? BrandColor.Parse(text) : null;
        if (rgb is null)
        {
            Add(path, BrandOverrideProblems.INVALID_COLOR, $"Use a hex, rgb(), hsl() or oklch() colour of at most {COLOR_MAX_LENGTH} characters.");
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
        if (!IsFileUnder(folder, value))
        {
            Add(path, BrandOverrideProblems.INVALID_ASSET_KEY, $"Must be a file under '{folder}', at most {ASSET_KEY_MAX_LENGTH} characters.");
        }
        else if (!_rasterExtensions.Any(extension => value.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        {
            Add(path, BrandOverrideProblems.UNSUPPORTED_ASSET_TYPE, "Must be a PNG or WebP file.");
        }
    }

    /// <summary>
    /// The control-character check stands beside the pack schema's pattern, whose <c>$</c> also matches before a final newline.
    /// </summary>
    private static bool IsFileUnder(string folder, string value) =>
        value.Length <= ASSET_KEY_MAX_LENGTH
        && !value.Any(char.IsControl)
        && value.StartsWith(folder, StringComparison.Ordinal)
        && FilePath().IsMatch(value[folder.Length..]);

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
        var unknown = value.EnumerateObject().Where(property => !allowed.Contains(property.Name, StringComparer.Ordinal));
        foreach (var property in unknown.TakeWhile(_ => _unknownFields++ < MAX_UNKNOWN_FIELDS))
        {
            Add($"{path}/{Escape(property.Name)}", BrandOverrideProblems.UNKNOWN_FIELD, "Is not a field a tenant can set.");
        }
    }

    /// <summary>JSON Pointer escaping (RFC 6901).</summary>
    private static string Escape(string name) => name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private void Add(string path, string code, string detail) =>
        _problems.Add(new ValidationError(path, detail, code, ValidationSeverity.Error));
}

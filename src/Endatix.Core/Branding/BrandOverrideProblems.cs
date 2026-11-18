namespace Endatix.Core.Branding;

/// <summary>
/// The codes of the problems a tenant brand override can have. A problem is a <c>ValidationError</c> whose
/// <c>Identifier</c> is the JSON Pointer of the field (<c>/tokens/color/primary/$value</c>), whose <c>ErrorCode</c> is one
/// of these, and whose <c>ErrorMessage</c> is the detail.
/// </summary>
/// <remarks>Clients match on these values, so they must not change.</remarks>
public static class BrandOverrideProblems
{
    /// <summary>The text is not JSON.</summary>
    public const string INVALID_JSON = "invalid_json";

    /// <summary>A required field is missing.</summary>
    public const string REQUIRED = "required";

    /// <summary>The field is not one a tenant may set. Name, links, personas, "Powered by" and email stay with the deployment.</summary>
    public const string UNKNOWN_FIELD = "unknown_field";

    /// <summary>The value has the wrong JSON type.</summary>
    public const string INVALID_TYPE = "invalid_type";

    /// <summary><c>version</c> is not a version this API reads.</summary>
    public const string UNSUPPORTED_VERSION = "unsupported_version";

    /// <summary>The colour is not a hex, <c>rgb()</c>, <c>hsl()</c> or <c>oklch()</c> colour.</summary>
    public const string INVALID_COLOR = "invalid_color";

    /// <summary>The accent's WCAG 2.x contrast on the page background is under 3:1 in light or dark mode.</summary>
    public const string LOW_CONTRAST = "low_contrast";

    /// <summary>The asset key is not a file under the tenant's own <c>t/{tenantId}/brand/</c> folder.</summary>
    public const string INVALID_ASSET_KEY = "invalid_asset_key";

    /// <summary>The asset is not a PNG or WebP file.</summary>
    public const string UNSUPPORTED_ASSET_TYPE = "unsupported_asset_type";

    /// <summary>A rollback found no previous version to return to.</summary>
    public const string NO_PREVIOUS_VERSION = "no_previous_version";
}

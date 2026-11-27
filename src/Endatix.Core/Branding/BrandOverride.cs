using System.Text.Json;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Versioning;

namespace Endatix.Core.Branding;

/// <summary>
/// A tenant's brand override, validated: the logo (light, optional dark), the icon and the accent (primary, optional
/// primary-on-dark), each optional. It has the brand pack's shape, version 1, limited to what a tenant may set, and its
/// assets are storage keys under the tenant's <c>t/{tenantId}/brand/</c> folder instead of pack files.
/// </summary>
/// <remarks>
/// Only <see cref="Parse"/> creates one, so a value of this type has passed every check: the shape, the accent's
/// WCAG 2.x contrast in light and dark mode (as the Hub derives it), and the asset keys. The API cannot see an asset's
/// size: the 1 MB limit is checked where the file is uploaded.
/// </remarks>
internal sealed class BrandOverride
{
    /// <summary>
    /// The longest override text read, in characters. A full override is a few hundred; the bound keeps the parse,
    /// the colour patterns and the problem list small whatever a caller sends.
    /// </summary>
    public const int MAX_JSON_LENGTH = 4096;

    private BrandOverride(long tenantId, string canonicalJson)
    {
        TenantId = tenantId;
        CanonicalJson = canonicalJson;
        ContentHash = CanonicalContentHash.Of(canonicalJson);
    }

    /// <summary>The tenant whose asset folder the keys were checked against.</summary>
    public long TenantId { get; }

    /// <summary>The override as JSON with sorted keys and no whitespace, so equal overrides have equal text.</summary>
    public string CanonicalJson { get; }

    /// <summary>The SHA-256 of <see cref="CanonicalJson"/>: the published version's content hash.</summary>
    public string ContentHash { get; }

    /// <summary>
    /// Validates <paramref name="json"/> as an override for <paramref name="tenantId"/>.
    /// </summary>
    /// <returns>
    /// The override, or <see cref="ResultStatus.Invalid"/> with one <see cref="ValidationError"/> per problem: the JSON Pointer
    /// in <see cref="ValidationError.Identifier"/>, a <see cref="BrandOverrideProblems"/> code in
    /// <see cref="ValidationError.ErrorCode"/>, and the detail in <see cref="ValidationError.ErrorMessage"/>.
    /// </returns>
    public static Result<BrandOverride> Parse(string json, long tenantId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenantId);
        ArgumentNullException.ThrowIfNull(json);

        var (document, refusal) = Read(json);
        if (document is null)
        {
            return refusal!;
        }

        using (document)
        {
            var problems = BrandOverrideReader.Check(document.RootElement, tenantId);
            return problems.Count > 0
                ? Result<BrandOverride>.Invalid(problems)
                : Result<BrandOverride>.Success(new BrandOverride(tenantId, Versioning.CanonicalJson.Write(document.RootElement)));
        }
    }

    /// <summary>The parsed text, or the problem with the text as a whole (its pointer is the empty string).</summary>
    private static (JsonDocument? Document, Result<BrandOverride>? Refusal) Read(string json)
    {
        if (json.Length > MAX_JSON_LENGTH)
        {
            return (null, RefusedWhole(BrandOverrideProblems.TOO_LARGE, $"Must be at most {MAX_JSON_LENGTH} characters."));
        }

        JsonDocument? document = null;
        try
        {
            document = JsonDocument.Parse(json, Versioning.CanonicalJson.Options);
            ReadEveryString(document.RootElement);
            return (document, null);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            document?.Dispose();
            return (null, RefusedWhole(BrandOverrideProblems.INVALID_JSON, "Is not valid JSON, repeats a key, or has an unpaired UTF-16 surrogate."));
        }
    }

    /// <summary>
    /// The parse accepts an unpaired surrogate escape (<c>"\uD800"</c>), but reading that string or property name throws
    /// <see cref="InvalidOperationException"/>; reading every one here makes the text invalid before any check reads it.
    /// A raw unpaired surrogate already fails the parse with <see cref="ArgumentException"/>.
    /// </summary>
    private static void ReadEveryString(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            _ = element.GetString();
        }

        var children = element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().Select(ValueAfterReadingName),
            JsonValueKind.Array => element.EnumerateArray().AsEnumerable(),
            _ => Enumerable.Empty<JsonElement>(),
        };
        foreach (var child in children)
        {
            ReadEveryString(child);
        }
    }

    private static JsonElement ValueAfterReadingName(JsonProperty property)
    {
        _ = property.Name;
        return property.Value;
    }

    private static Result<BrandOverride> RefusedWhole(string code, string detail) =>
        Result<BrandOverride>.Invalid(new ValidationError("", detail, code, ValidationSeverity.Error));
}

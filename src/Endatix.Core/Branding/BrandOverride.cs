using System.Text.Json;
using Endatix.Core.Infrastructure.Result;

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
public sealed class BrandOverride
{
    private BrandOverride(long tenantId, string canonicalJson)
    {
        TenantId = tenantId;
        CanonicalJson = canonicalJson;
    }

    /// <summary>The tenant whose asset folder the keys were checked against.</summary>
    public long TenantId { get; }

    /// <summary>The override as JSON with sorted keys and no whitespace, so equal overrides have equal text.</summary>
    public string CanonicalJson { get; }

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

        if (TryParseDocument(json) is not { } document)
        {
            return Result<BrandOverride>.Invalid(
                new ValidationError("", "Is not valid JSON, or repeats a key.", BrandOverrideProblems.INVALID_JSON, ValidationSeverity.Error));
        }

        using (document)
        {
            var problems = BrandOverrideReader.Check(document.RootElement, tenantId);
            return problems.Count > 0
                ? Result<BrandOverride>.Invalid(problems)
                : Result<BrandOverride>.Success(new BrandOverride(tenantId, CanonicalBrandJson.Write(document.RootElement)));
        }
    }

    /// <summary>
    /// Whether <paramref name="storedJson"/> holds this override. A database may store JSON with its keys reordered,
    /// so the stored text is compared in canonical form.
    /// </summary>
    public bool IsSameAs(string? storedJson) =>
        storedJson is not null && CanonicalBrandJson.Write(storedJson) == CanonicalJson;

    private static JsonDocument? TryParseDocument(string json)
    {
        try
        {
            return JsonDocument.Parse(json, CanonicalBrandJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

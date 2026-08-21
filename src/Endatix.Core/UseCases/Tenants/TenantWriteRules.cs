using Endatix.Core.Common;
using Endatix.Core.Infrastructure.Result;
using Entities = Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Tenants;

/// <summary>
/// Write rules shared by the tenant create and update use cases.
/// </summary>
public static class TenantWriteRules
{
    /// <summary>
    /// Normalizes an explicit tenant slug and rejects empty, malformed, and reserved values.
    /// </summary>
    public static Result<string> NormalizeSlug(string? rawSlug, string fallbackName)
    {
        var slug = string.IsNullOrWhiteSpace(rawSlug)
            ? UrlSlugNormalizer.FromDisplayName(fallbackName)
            : UrlSlugNormalizer.Normalize(rawSlug);

        if (string.IsNullOrEmpty(slug))
        {
            return Result.Error("Slug cannot be empty.");
        }

        if (!UrlSlugNormalizer.IsValidFormat(slug))
        {
            return Result.Error("Slug format is invalid. Use lowercase letters, numbers, and hyphens only.");
        }

        if (UrlSlugNormalizer.IsReserved(slug))
        {
            return Result.Error("This slug is reserved.");
        }

        return Result.Success(slug);
    }

    public static ValidationError InvalidName(string identifier) => new()
    {
        Identifier = identifier,
        ErrorMessage = "Tenant name cannot be empty."
    };

    public static ValidationError InvalidSlug(string message, string identifier) => new()
    {
        Identifier = identifier,
        ErrorMessage = message
    };

    public static ValidationError DuplicateSlug(string slug, string identifier) => new()
    {
        Identifier = identifier,
        ErrorMessage = $"A tenant with the slug '{slug}' already exists."
    };

    public static ValidationError ForbiddenRegistrationRole(string roleName, string identifier) => new()
    {
        Identifier = identifier,
        ErrorMessage = $"Default registration role '{roleName}' is not allowed. Use a persisted tenant role (default: {Entities.TenantSettings.DefaultRegistrationRole})."
    };
}

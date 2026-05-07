using Endatix.Core.Common;
using FluentValidation;

namespace Endatix.Api.Common;

/// <summary>
/// Extension methods for validating JSON strings.
/// </summary>
public static class FluentValidationExtensions
{
    /// <summary>
    /// Validates that the string is a valid JSON string.
    /// </summary>
    /// <typeparam name="T">The type of the object being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder.</param>
    /// <returns>The rule builder.</returns>
    public static IRuleBuilderOptions<T, string?> ValidJsonString<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .Must(JsonStringValidation.IsValid)
            .WithMessage("{PropertyName} must be a valid JSON string.");
    }

    /// <summary>
    /// Validates that the string is a valid URL slug.
    /// </summary>
    /// <typeparam name="T">The type of the object being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder.</param>
    /// <returns>The rule builder.</returns>
    public static IRuleBuilderOptions<T, string?> ValidUrlSlug<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .MaximumLength(UrlSlugNormalizer.MAX_SLUG_LENGTH)
            .Must(slug => UrlSlugNormalizer.IsValidFormat(slug))
            .WithMessage("{PropertyName} must be a valid URL slug.");
    }
}

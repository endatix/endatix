using FluentValidation;

namespace Endatix.Api.Common;

/// <summary>
/// Extension methods for validating JSON strings.
/// </summary>
public static class JsonStringValidationExtensions
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
}

using FluentValidation;

namespace Endatix.Api.Endpoints.Forms;

internal static class JsonStringValidationExtensions
{
    internal static IRuleBuilderOptions<T, string?> MustBeValidJsonString<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .Must(JsonStringValidation.IsValid)
            .WithMessage("Metadata must be a valid JSON string.");
    }
}

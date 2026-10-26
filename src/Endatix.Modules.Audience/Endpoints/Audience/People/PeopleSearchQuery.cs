using Endatix.Core.Common;
using FluentValidation;

namespace Endatix.Modules.Audience.Endpoints.Audience.People;

/// <summary>
/// The people search a request sends: part of an identifier, so after trimming at most as long as one.
/// </summary>
internal static class PeopleSearchQuery
{
    public static IRuleBuilderOptions<T, string?> ValidPeopleSearch<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(search => search is null || search.Trim().Length <= DataSchemaConstants.MAX_EMAIL_LENGTH)
            .WithMessage($"Search must be at most {DataSchemaConstants.MAX_EMAIL_LENGTH} characters.");
}

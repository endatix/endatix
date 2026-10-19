using Endatix.Modules.Audience.Features.People;
using FluentValidation;

namespace Endatix.Modules.Audience.Endpoints.Audience.People;

/// <summary>
/// The people list's <c>test</c> query value: <c>all</c> (or absent), <c>real</c> or <c>test</c>, in lowercase.
/// </summary>
internal static class PeopleTestFilterQuery
{
    /// <summary>The filter <paramref name="value"/> names, or <see langword="null"/> when it names none.</summary>
    public static PeopleTestFilter? Parse(string? value) =>
        value switch
        {
            null or "" or "all" => PeopleTestFilter.All,
            "real" => PeopleTestFilter.Real,
            "test" => PeopleTestFilter.Test,
            _ => null,
        };

    public static IRuleBuilderOptions<T, string?> ValidPeopleTestFilter<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => Parse(value) is not null)
            .WithMessage("Test must be all, real or test.");
}

using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Validator for the GetFormsByThemeIdRequest.
/// </summary>
public class GetFormsByThemeIdValidator : Validator<GetFormsByThemeIdRequest>
{
    public GetFormsByThemeIdValidator()
    {
        RuleFor(x => x.ThemeId)
            .GreaterThan(0);
    }
}
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Themes;
public class GetByIdValidator : Validator<GetByIdRequest>
{
    public GetByIdValidator()
    {
        RuleFor(x => x.ThemeId)
            .GreaterThan(0);
    }
}
using Endatix.Api.Endpoints.Themes;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Themes;
public class DeleteValidator : Validator<DeleteRequest>
{
    public DeleteValidator()
    {
        RuleFor(x => x.ThemeId)
            .GreaterThan(0);
    }
}
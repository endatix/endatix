using Endatix.Infrastructure.Data.Config;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Themes;

public class UpdateValidator : Validator<UpdateRequest>
{
    public UpdateValidator()
    {
        RuleFor(x => x.ThemeId)
            .GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_NAME_LENGTH)
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);

        RuleFor(x => x.JsonData)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_JSON_LENGTH);
    }
}
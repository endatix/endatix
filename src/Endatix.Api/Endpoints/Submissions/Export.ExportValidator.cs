using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Submissions;

public class ExportValidator : Validator<ExportRequest>
{
    public ExportValidator()
    {
        RuleFor(x => x.FormId)
             .GreaterThan(0);
    }
}

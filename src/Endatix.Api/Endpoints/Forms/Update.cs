using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms.Update;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for updating a form.
/// </summary>
public class Update(IMediator mediator) : Endpoint<UpdateFormRequest, Results<Ok<UpdateFormResponse>, BadRequest, NotFound, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Put("forms/{formId}");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = "Update a form";
            s.Description = "Updates a form.";
            s.Responses[200] = "Form updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<UpdateFormResponse>, BadRequest, NotFound, ProblemHttpResult>> ExecuteAsync(UpdateFormRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateFormCommand(
                request.FormId,
                request.Name!,
                request.Description,
                request.IsEnabled!.Value,
                request.WebHookSettingsJson,
                request.LimitOnePerUser,
                request.Metadata),
            cancellationToken);

        var mappedResult = result.Map(FormMapper.Map<UpdateFormResponse>);

        return mappedResult.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(mappedResult.Value),
            ResultStatus.Invalid => TypedResults.BadRequest(),
            ResultStatus.NotFound => TypedResults.NotFound(),
            _ => mappedResult.ToProblem()
        };
    }
}

using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Forms.Update;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for updating a form.
/// </summary>
public class Update(IMediator mediator) : Endpoint<UpdateFormRequest, Results<Ok<UpdateFormResponse>, BadRequest, NotFound>>
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
    public override async Task<Results<Ok<UpdateFormResponse>, BadRequest, NotFound>> ExecuteAsync(UpdateFormRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateFormCommand(request.FormId, request.Name!, request.Description, request.IsEnabled!.Value, request.WebHookSettingsJson),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, FormMapper.Map<UpdateFormResponse>)
            .SetTypedResults<Ok<UpdateFormResponse>, BadRequest, NotFound>();
    }
}

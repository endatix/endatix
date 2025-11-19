using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Forms.PartialUpdate;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for partially updating a form.
/// </summary>
public class PartialUpdate(IMediator mediator) : Endpoint<PartialUpdateFormRequest, Results<Ok<PartialUpdateFormResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("forms/{formId}");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = "Partially update a form";
            s.Description = "Partially updates a form.";
            s.Responses[200] = "Form updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<PartialUpdateFormResponse>, BadRequest, NotFound>> ExecuteAsync(PartialUpdateFormRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new PartialUpdateFormCommand(request.FormId, request.Name, request.Description, request.IsEnabled, request.ThemeId, request.WebHookSettingsJson),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, FormMapper.Map<PartialUpdateFormResponse>)
            .SetTypedResults<Ok<PartialUpdateFormResponse>, BadRequest, NotFound>();
    }
}

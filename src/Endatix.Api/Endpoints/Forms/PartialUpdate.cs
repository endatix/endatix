using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Forms.PartialUpdate;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for partially updating a form.
/// </summary>
public class PartialUpdate(IMediator _mediator) : Endpoint<PartialUpdateFormRequest, Results<Ok<PartialUpdateFormResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("forms/{formId}");
        Roles("Admin");
        Summary(s =>
        {
            s.Summary = "Partially update a form";
            s.Description = "Partially updates a form.";
            s.Responses[200] = "Form updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <summary>
    /// Executes the HTTP request for partially updating a form.
    /// </summary>
    /// <param name="request">The request model containing form details.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public override async Task<Results<Ok<PartialUpdateFormResponse>, BadRequest, NotFound>> ExecuteAsync(PartialUpdateFormRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PartialUpdateFormCommand(request.FormId, request.Name, request.Description, request.IsEnabled),
            cancellationToken);

        return result.ToEndpointResponse<
            Results<Ok<PartialUpdateFormResponse>, BadRequest, NotFound>,
            Form,
            PartialUpdateFormResponse>(FormMapper.Map<PartialUpdateFormResponse>);
    }
}

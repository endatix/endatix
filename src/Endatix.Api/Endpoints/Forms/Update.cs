using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Forms.Update;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for updating a form.
/// </summary>
public class Update(IMediator _mediator) : Endpoint<UpdateFormRequest, Results<Ok<UpdateFormResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Put("forms/{formId}");
        Roles("Admin");
        Summary(s =>
        {
            s.Summary = "Update a form";
            s.Description = "Updates a form.";
            s.Responses[200] = "Form updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <summary>
    /// Executes the HTTP request for updating a form.
    /// </summary>
    /// <param name="request">The request model containing form details.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public override async Task<Results<Ok<UpdateFormResponse>, BadRequest, NotFound>> ExecuteAsync(UpdateFormRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UpdateFormCommand(request.FormId, request.Name!, request.Description, request.IsEnabled!.Value),
            cancellationToken);

        return result.ToEndpointResponse<
            Results<Ok<UpdateFormResponse>, BadRequest, NotFound>,
            Form,
            UpdateFormResponse>(FormMapper.Map<UpdateFormResponse>);
    }
}

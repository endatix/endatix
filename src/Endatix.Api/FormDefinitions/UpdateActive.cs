using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.FormDefinitions.UpdateActive;

namespace Endatix.Api.FormDefinitions;

/// <summary>
/// Endpoint for updating the active form definition.
/// </summary>
public class UpdateActive(IMediator _mediator) : Endpoint<UpdateActiveFormDefinitionRequest, Results<Ok<UpdateActiveFormDefinitionResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Put("forms/{formId}/definition");
        Roles("Admin");
        Summary(s =>
        {
            s.Summary = "Update the active form definition";
            s.Description = "Updates the active form definition for a given form.";
            s.Responses[200] = "Active form definition updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <summary>
    /// Executes the HTTP request for updating the active form definition.
    /// </summary>
    /// <param name="request">The request model containing active form definition details.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public override async Task<Results<Ok<UpdateActiveFormDefinitionResponse>, BadRequest, NotFound>> ExecuteAsync(UpdateActiveFormDefinitionRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UpdateActiveFormDefinitionCommand(request.FormId, request.IsDraft!.Value, request.JsonData!, request.IsActive!.Value),
            cancellationToken);

        return result.ToEndpointResponse<
            Results<Ok<UpdateActiveFormDefinitionResponse>, BadRequest, NotFound>,
            FormDefinition,
            UpdateActiveFormDefinitionResponse>(FormDefinitionMapper.Map<UpdateActiveFormDefinitionResponse>);
    }
}

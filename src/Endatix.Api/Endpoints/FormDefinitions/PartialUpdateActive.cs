using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.FormDefinitions.PartialUpdateActive;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for partially updating the active form definition.
/// </summary>
public class PartialUpdateActive(IMediator _mediator) : Endpoint<PartialUpdateActiveFormDefinitionRequest, Results<Ok<PartialUpdateActiveFormDefinitionResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("forms/{formId}/definition");
        Roles("Admin");
        Summary(s =>
        {
            s.Summary = "Partially update the active form definition";
            s.Description = "Partially updates the active form definition for a given form.";
            s.Responses[200] = "Active form definition updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <summary>
    /// Executes the HTTP request for partially updating the active form definition.
    /// </summary>
    /// <param name="request">The request model containing active form definition details.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public override async Task<Results<Ok<PartialUpdateActiveFormDefinitionResponse>, BadRequest, NotFound>> ExecuteAsync(PartialUpdateActiveFormDefinitionRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PartialUpdateActiveFormDefinitionCommand(request.FormId, request.IsDraft, request.JsonData, request.IsActive),
            cancellationToken);

        return result.ToEndpointResponse<
            Results<Ok<PartialUpdateActiveFormDefinitionResponse>, BadRequest, NotFound>,
            FormDefinition,
            PartialUpdateActiveFormDefinitionResponse>(FormDefinitionMapper.Map<PartialUpdateActiveFormDefinitionResponse>);
    }
}

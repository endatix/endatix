using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.FormDefinitions.PartialUpdate;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for partially updating a form definition.
/// </summary>
public class PartialUpdate(IMediator _mediator) : Endpoint<PartialUpdateFormDefinitionRequest, Results<Ok<PartialUpdateFormDefinitionResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("forms/{formId}/definitions/{definitionId}");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "Partially update a form definition";
            s.Description = "Partially updates a form definition for a given form.";
            s.Responses[200] = "Form definition updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form definition or form not found.";
        });
    }

    /// <summary>
    /// Executes the HTTP request for partially updating a form definition.
    /// </summary>
    /// <param name="request">The request model containing form definition details.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public override async Task<Results<Ok<PartialUpdateFormDefinitionResponse>, BadRequest, NotFound>> ExecuteAsync(PartialUpdateFormDefinitionRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PartialUpdateFormDefinitionCommand(request.FormId, request.DefinitionId, request.IsDraft, request.JsonData, request.IsActive),
            cancellationToken);

        return result.ToEndpointResponse<
            Results<Ok<PartialUpdateFormDefinitionResponse>, BadRequest, NotFound>,
            FormDefinition,
            PartialUpdateFormDefinitionResponse>(FormDefinitionMapper.Map<PartialUpdateFormDefinitionResponse>);
    }
}

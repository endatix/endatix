using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.FormDefinitions.Update;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for updating a form definition.
/// </summary>
public class Update(IMediator _mediator) : Endpoint<UpdateFormDefinitionRequest, Results<Ok<UpdateFormDefinitionResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Put("forms/{formId}/definitions/{definitionId}");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "Update a form definition";
            s.Description = "Updates a form definition for a given form.";
            s.Responses[200] = "Form definition updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form definition or form not found.";
        });
    }

    /// <summary>
    /// Executes the HTTP request for updating a form definition.
    /// </summary>
    /// <param name="request">The request model containing form definition details.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public override async Task<Results<Ok<UpdateFormDefinitionResponse>, BadRequest, NotFound>> ExecuteAsync(UpdateFormDefinitionRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UpdateFormDefinitionCommand(request.FormId, request.DefinitionId, request.IsDraft!.Value, request.JsonData!, request.IsActive!.Value),
            cancellationToken);

        return result.ToEndpointResponse<
            Results<Ok<UpdateFormDefinitionResponse>, BadRequest, NotFound>,
            FormDefinition,
            UpdateFormDefinitionResponse>(FormDefinitionMapper.Map<UpdateFormDefinitionResponse>);
    }
}

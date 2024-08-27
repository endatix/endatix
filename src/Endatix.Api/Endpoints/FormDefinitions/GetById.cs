using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.FormDefinitions.GetById;

namespace Endatix.Api.FormDefinitions;

/// <summary>
/// Endpoint for getting a form definition by ID.
/// </summary>
public class GetById(IMediator _mediator) : Endpoint<GetFormDefinitionByIdRequest, Results<Ok<FormDefinitionModel>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms/{formId}/definitions/{definitionId}");
        Roles("Admin");
        Summary(s =>
        {
            s.Summary = "Get a form definition by ID";
            s.Description = "Gets a form definition by its ID for a given form.";
            s.Responses[200] = "Form definition retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form definition or form not found.";
        });
    }

    /// <summary>
    /// Executes the HTTP request for getting a form definition by ID.
    /// </summary>
    /// <param name="request">The request model containing the form and definition IDs.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public override async Task<Results<Ok<FormDefinitionModel>, BadRequest, NotFound>> ExecuteAsync(GetFormDefinitionByIdRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetFormDefinitionByIdQuery(request.FormId, request.DefinitionId),
            cancellationToken);

        return result.ToEndpointResponse<
            Results<Ok<FormDefinitionModel>, BadRequest, NotFound>,
            FormDefinition,
            FormDefinitionModel>(FormDefinitionMapper.Map<FormDefinitionModel>);
    }
}

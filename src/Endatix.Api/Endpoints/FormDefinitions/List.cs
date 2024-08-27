using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.FormDefinitions.List;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for listing form definitions.
/// </summary>
public class List(IMediator _mediator) : Endpoint<FormDefinitionsListRequest, Results<Ok<IEnumerable<FormDefinitionModel>>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms/{formId}/definitions");
        Roles("Admin");
        Summary(s =>
        {
            s.Summary = "List form definitions";
            s.Description = "Lists all form definitions for a given form with optional pagination.";
            s.Responses[200] = "Form definitions retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <summary>
    /// Executes the HTTP request for listing form definitions.
    /// </summary>
    /// <param name="request">The request model containing form ID and pagination details.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public override async Task<Results<Ok<IEnumerable<FormDefinitionModel>>, BadRequest, NotFound>> ExecuteAsync(FormDefinitionsListRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ListFormDefinitionsQuery(request.FormId, request.Page, request.PageSize),
            cancellationToken);

        return result.ToEndpointResponse<
            Results<Ok<IEnumerable<FormDefinitionModel>>, BadRequest, NotFound>,
            IEnumerable<FormDefinition>,
            IEnumerable<FormDefinitionModel>>(FormDefinitionMapper.Map<FormDefinitionModel>);
    }
}

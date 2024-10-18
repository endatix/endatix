using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.FormDefinitions.Create;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for creating a new form definition.
/// </summary>
public class Create(IMediator _mediator) : Endpoint<CreateFormDefinitionRequest, Results<Created<CreateFormDefinitionResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("forms/{formId}/definitions");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "Create a new form definition";
            s.Description = "Creates a new form definition for a given form.";
            s.Responses[201] = "Form definition created successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <summary>
    /// Executes the HTTP request for creating a new form definition.
    /// </summary>
    /// <param name="request">The request model containing form definition details.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public override async Task<Results<Created<CreateFormDefinitionResponse>, BadRequest, NotFound>> ExecuteAsync(CreateFormDefinitionRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new CreateFormDefinitionCommand(request.FormId, request.IsDraft!.Value, request.JsonData!, request.IsActive!.Value),
            cancellationToken);
        
        return result.ToEndpointResponse<
            Results<Created<CreateFormDefinitionResponse>, BadRequest, NotFound>,
            FormDefinition,
            CreateFormDefinitionResponse>(FormDefinitionMapper.Map<CreateFormDefinitionResponse>);
    }
}

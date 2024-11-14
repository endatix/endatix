using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormDefinitions.PartialUpdateActive;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Endpoint for partially updating the active form definition.
/// </summary>
public class PartialUpdateActive(IMediator mediator) : Endpoint<PartialUpdateActiveFormDefinitionRequest, Results<Ok<PartialUpdateActiveFormDefinitionResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("forms/{formId}/definition");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "Partially update the active form definition";
            s.Description = "Partially updates the active form definition for a given form.";
            s.Responses[200] = "Active form definition updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<PartialUpdateActiveFormDefinitionResponse>, BadRequest, NotFound>> ExecuteAsync(PartialUpdateActiveFormDefinitionRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new PartialUpdateActiveFormDefinitionCommand(request.FormId, request.IsDraft, request.JsonData, request.IsActive),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, FormDefinitionMapper.Map<PartialUpdateActiveFormDefinitionResponse>)
            .SetTypedResults<Ok<PartialUpdateActiveFormDefinitionResponse>, BadRequest, NotFound>();
    }
}
